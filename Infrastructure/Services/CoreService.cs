using System.Collections.Concurrent;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public class CoreService : IDisposable
{
    private PowerShellSession? _generalSession;
    private PowerShellSession? _installerSession;

    private readonly SemaphoreSlim _generalSemaphore = new(1, 1);
    private readonly SemaphoreSlim _installerSemaphore = new(1, 1);

    public CoreService() { }

    public enum PowerShellLane
    {
        General,
        Installer,
    }

    public async Task<(bool Success, string Output, string Error)> ExecutePowerShellAsync(
        string command,
        PowerShellLane lane = PowerShellLane.General
    )
    {
        PowerShellSession? session = null;

        var semaphore = lane == PowerShellLane.Installer ? _installerSemaphore : _generalSemaphore;

        await semaphore.WaitAsync();

        try
        {
            if (lane == PowerShellLane.Installer)
            {
                if (_installerSession == null)
                    _installerSession = new PowerShellSession();
                session = _installerSession;
            }
            else
            {
                if (_generalSession == null)
                    _generalSession = new PowerShellSession();
                session = _generalSession;
            }

            return await session.ExecuteCommandAsync(command);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<bool> CreateSystemRestorePointAsync(string description)
    {
        string cmd =
            $"Checkpoint-Computer -Description \"{description}\" -RestorePointType \"MODIFY_SETTINGS\"";
        var (success, _, _) = await ExecutePowerShellAsync(cmd);
        return success;
    }

    public void Dispose()
    {
        _generalSession?.Dispose();
        _installerSession?.Dispose();
        _generalSemaphore.Dispose();
        _installerSemaphore.Dispose();
    }
}
