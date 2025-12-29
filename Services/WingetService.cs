using System.Collections.Concurrent;
using System.Diagnostics;

namespace WinsaneCS.Services;

/// <summary>
/// Service for managing winget installations with queue to prevent concurrent installs
/// </summary>
public class WingetService
{
    private readonly ConcurrentQueue<WingetTask> _taskQueue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public event EventHandler<WingetProgressEventArgs>? ProgressChanged;
    public event EventHandler<WingetCompletedEventArgs>? TaskCompleted;
    
    /// <summary>
    /// Queue an install or uninstall task
    /// </summary>
    public async Task QueueTaskAsync(string packageId, bool install, string displayName)
    {
        var task = new WingetTask
        {
            PackageId = packageId,
            Install = install,
            DisplayName = displayName
        };
        
        _taskQueue.Enqueue(task);
        await ProcessQueueAsync();
    }
    
    private async Task ProcessQueueAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            while (_taskQueue.TryDequeue(out var task))
            {
                await ExecuteTaskAsync(task);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task ExecuteTaskAsync(WingetTask task)
    {
        ProgressChanged?.Invoke(this, new WingetProgressEventArgs
        {
            PackageId = task.PackageId,
            DisplayName = task.DisplayName,
            Status = task.Install ? "Installing..." : "Uninstalling..."
        });
        
        var success = await RunWingetAsync(task);
        
        TaskCompleted?.Invoke(this, new WingetCompletedEventArgs
        {
            PackageId = task.PackageId,
            DisplayName = task.DisplayName,
            Success = success,
            WasInstall = task.Install
        });
    }
    
    private async Task<bool> RunWingetAsync(WingetTask task)
    {
        return await Task.Run(() =>
        {
            try
            {
                var args = task.Install
                    ? $"install --id={task.PackageId} -e --accept-source-agreements --accept-package-agreements --silent"
                    : $"uninstall --id={task.PackageId} -e --disable-interactivity --silent";
                
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
                
                if (!process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
                {
                    try { process.Kill(); } catch {}
                    return false;
                }
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Winget error: {ex.Message}");
                return false;
            }
        });
    }
}

public class WingetTask
{
    public string PackageId { get; set; } = string.Empty;
    public bool Install { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class WingetProgressEventArgs : EventArgs
{
    public string PackageId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class WingetCompletedEventArgs : EventArgs
{
    public string PackageId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool WasInstall { get; set; }
}
