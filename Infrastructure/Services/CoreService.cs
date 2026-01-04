using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

/// <summary>
/// Unified Core Service handling System operations, Winget management, and Backup functionality.
/// </summary>
public class CoreService
{
    // --- Winget Queue ---
    private readonly ConcurrentQueue<(WingetTask Task, TaskCompletionSource<bool> Tcs)> _taskQueue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public event EventHandler<WingetProgressEventArgs>? WingetProgressChanged;
    public event EventHandler<WingetCompletedEventArgs>? WingetTaskCompleted;

    // --- PowerShell / Process Execution ---
    
    /// <summary>
    /// Execute a PowerShell command asynchronously.
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> ExecutePowerShellAsync(string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = CreateStartInfo(command, asAdmin: false);

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        });
    }

    /// <summary>
    /// Execute a PowerShell command as administrator (elevated).
    /// </summary>
    public async Task<bool> ExecutePowerShellAsAdminAsync(string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = CreateStartInfo(command, asAdmin: true);
                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        });
    }

    private ProcessStartInfo CreateStartInfo(string command, bool asAdmin)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = asAdmin, 
            CreateNoWindow = true
        };

        if (asAdmin)
        {
            startInfo.Verb = "runas";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        }
        else
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
        }

        return startInfo;
    }

    // --- Winget Logic ---

    public Task<bool> QueueWingetTaskAsync(string packageId, bool install, string displayName)
    {
        var task = new WingetTask
        {
            PackageId = packageId,
            Install = install,
            DisplayName = displayName
        };
        
        var tcs = new TaskCompletionSource<bool>();
        _taskQueue.Enqueue((task, tcs));
        
        _ = ProcessWingetQueueAsync();
        
        return tcs.Task;
    }
    
    private async Task ProcessWingetQueueAsync()
    {
        if (!await _semaphore.WaitAsync(0)) return;

        try
        {
            while (_taskQueue.TryDequeue(out var item))
            {
                var (task, tcs) = item;
                try
                {
                     WingetProgressChanged?.Invoke(this, new WingetProgressEventArgs
                    {
                        PackageId = task.PackageId,
                        DisplayName = task.DisplayName,
                        Status = task.Install ? "Installing..." : "Uninstalling..."
                    });
                    
                    bool success = await RunWingetCommandAsync(task);
                    
                    WingetTaskCompleted?.Invoke(this, new WingetCompletedEventArgs
                    {
                        PackageId = task.PackageId,
                        DisplayName = task.DisplayName,
                        Success = success,
                        WasInstall = task.Install
                    });
                    
                    tcs.SetResult(success);
                }
                catch (Exception ex)
                {
                    tcs.SetResult(false);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task<bool> RunWingetCommandAsync(WingetTask task)
    {
        return await Task.Run(() =>
        {
            try
            {
                var args = task.Install
                    ? $"install --id {task.PackageId} -e --accept-source-agreements --accept-package-agreements --silent --force"
                    : $"uninstall --id {task.PackageId} -e --accept-source-agreements --silent";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                if (!process.WaitForExit((int)TimeSpan.FromMinutes(15).TotalMilliseconds))
                {
                    try { process.Kill(); } catch { /* Process already terminated */ }
                    return false;
                }
                
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> IsWingetInstalledAsync(string packageId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"list --id {packageId} --exact --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                process.WaitForExit(5000); 
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        });
    }

    // --- Backup Logic ---

    public async Task<bool> CreateSystemRestorePointAsync(string description)
    {
        string cmd = $"Checkpoint-Computer -Description \"{description}\" -RestorePointType \"MODIFY_SETTINGS\"";
        return await ExecutePowerShellAsAdminAsync(cmd);
    }
    // --- UAC Suppression Logic ---
    
    private const string UacKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private int? _originalUacValue;
    
    public void SuppressUac()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(UacKey, true);
            if (key == null) return;
            
            var current = key.GetValue("ConsentPromptBehaviorAdmin");
            if (current is int val)
            {
                _originalUacValue = val;
            }
            
            // Set to 0: Elevate without prompting
            key.SetValue("ConsentPromptBehaviorAdmin", 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            // Silent fail - UAC might already be disabled or insufficient permissions
        }
    }
    
    public void RestoreUac()
    {
        if (_originalUacValue == null) return;
        
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(UacKey, true);
            if (key == null) return;
            
            key.SetValue("ConsentPromptBehaviorAdmin", _originalUacValue.Value, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            // Silent fail - UAC restoration is best-effort
        }
    }
}
