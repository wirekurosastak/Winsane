using System.Diagnostics;

namespace WinsaneCS.Services;

/// <summary>
/// Service for executing PowerShell commands
/// </summary>
public class PowerShellService
{
    /// <summary>
    /// Execute a PowerShell command asynchronously
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> ExecuteAsync(string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

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
    /// Execute a PowerShell command as administrator (elevated)
    /// </summary>
    public async Task<bool> ExecuteAsAdminAsync(string command)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Start a process (for Admin Tools - opens consoles)
    /// </summary>
    public void StartProcess(string command)
    {
        try
        {
            var parts = command.Split(' ', 2);
            var fileName = parts[0].Replace("Start-Process ", "").Trim();
            
            // Handle PowerShell Start-Process commands
            if (command.StartsWith("Start-Process"))
            {
                var processName = command.Replace("Start-Process", "").Trim();
                
                // Check for -Verb RunAs
                bool runAsAdmin = processName.Contains("-Verb RunAs");
                processName = processName.Replace("-Verb RunAs", "").Trim();

                var startInfo = new ProcessStartInfo
                {
                    FileName = processName,
                    UseShellExecute = true
                };

                if (runAsAdmin)
                {
                    startInfo.Verb = "runas";
                }

                Process.Start(startInfo);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start process: {ex.Message}");
        }
    }
}
