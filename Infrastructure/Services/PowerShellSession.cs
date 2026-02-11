using System.Diagnostics;
using System.Text;

namespace Winsane.Infrastructure.Services;

public class PowerShellSession : IDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

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

            Arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -Command -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();
    }

    public async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(
        string command
    )
    {
        if (_disposed || _process == null || _process.HasExited)
        {
            try
            {
                _process?.Dispose();
            }
            catch { }
            StartProcess();
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_process?.StandardInput == null)
                return (false, "", "Process not started");

            var wrapper =
                $@"
$OutputEncoding = [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Error.Clear()
try {{
    & {{ {command} }}
}} catch {{
    Write-Error $_
}}
Write-Output '{EndOfCommandToken}'
";

            await _process.StandardInput.WriteLineAsync(wrapper);
            await _process.StandardInput.FlushAsync();

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            while (true)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line == null)
                    break;

                if (line.Contains(EndOfCommandToken))
                {
                    break;
                }

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
        if (_disposed)
            return;
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
