using System.Diagnostics;
using System.Text;

namespace Winsane.Infrastructure.Services;

/// <summary>
/// Manages a single persistent PowerShell process.
/// Commands are piped directly to Stdin to avoid process creation overhead.
/// </summary>
public class PowerShellSession : IDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;
    
    // Unique token to identify the end of a command execution stream
    private const string EndOfCommandToken = "---WINSANE-END-OF-EXECUTION---";

    public PowerShellSession()
    {
        StartProcess();
    }

    private void StartProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            // -Command - : Reads commands from StandardInput
            // -NoProfile : Faster startup
            Arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -Command -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true, // Capture standard error too
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8, // Explicitly use UTF8 for input
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();
    }

    /// <summary>
    /// Executes a command in the persistent session.
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command)
    {
        if (_disposed || _process == null || _process.HasExited)
        {
             try { _process?.Dispose(); } catch { }
             StartProcess();
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_process?.StandardInput == null) return (false, "", "Process not started");

            // We wrap the command to ensure:
            // 1. Errors are caught and printed.
            // 2. We print a specific token at the end so we know when to stop reading.
            // 3. We use UTF8 for encoding reliability.
            var wrapper = $@"
$OutputEncoding = [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Error.Clear()
try {{
    & {{ {command} }}
}} catch {{
    Write-Error $_
}}
Write-Output '{EndOfCommandToken}'
";
            
            // Send command
            await _process.StandardInput.WriteLineAsync(wrapper);
            await _process.StandardInput.FlushAsync();

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder(); // Captured via Write-Error in stream

            while (true)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line == null) break; // Process died unexpectedly
                
                if (line.Contains(EndOfCommandToken))
                {
                    break;
                }
                
                // Simple heuristic: If line looks like a PS error, track it as error, otherwise output
                // (In a real scenario, we might merge streams 2>&1, but this is sufficient for tweaks)
                if (line.Contains(" : ") && (line.Contains("Error") || line.Contains("Exception")))
                {
                    errorBuilder.AppendLine(line);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    outputBuilder.AppendLine(line);
                }
            }
            
            var errorStr = errorBuilder.ToString().Trim();
            var outputStr = outputBuilder.ToString().Trim();
            
            // Success if no errors were captured
            return (string.IsNullOrEmpty(errorStr), outputStr, errorStr);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
            }
        }
        catch { }
    }
}