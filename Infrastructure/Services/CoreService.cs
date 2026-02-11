using System.Collections.Concurrent;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public class CoreService : IDisposable
{
    private readonly ConcurrentBag<PowerShellSession> _sessionPool = new();
    private readonly SemaphoreSlim _poolSemaphore;
    
    private PowerShellSession? _installerSession;
    private readonly SemaphoreSlim _installerSemaphore = new(1, 1);
    
    private bool _disposed;

    public CoreService() 
    { 
        // Allow up to ProcessorCount sessions for parallel checks
        int poolSize = Math.Max(2, Environment.ProcessorCount);
        _poolSemaphore = new SemaphoreSlim(poolSize, poolSize);
    }

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
        if (lane == PowerShellLane.Installer)
        {
            return await ExecuteInstallerCommandAsync(command);
        }

        return await ExecutePooledCommandAsync(command);
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteInstallerCommandAsync(string command)
    {
        await _installerSemaphore.WaitAsync();
        try
        {
            if (_installerSession == null)
                _installerSession = new PowerShellSession();
            
            return await _installerSession.ExecuteCommandAsync(command);
        }
        finally
        {
            _installerSemaphore.Release();
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecutePooledCommandAsync(string command)
    {
        await _poolSemaphore.WaitAsync();
        
        PowerShellSession? session = null;
        try
        {
            if (!_sessionPool.TryTake(out session))
            {
                session = new PowerShellSession();
            }

            return await session.ExecuteCommandAsync(command);
        }
        finally
        {
            if (session != null && !_disposed)
            {
                _sessionPool.Add(session);
            }
            else
            {
                session?.Dispose();
            }
            _poolSemaphore.Release();
        }
    }

    public async Task<bool> CreateSystemRestorePointAsync(string description)
    {
        string cmd =
            $"Checkpoint-Computer -Description \"{description}\" -RestorePointType \"MODIFY_SETTINGS\"";
        var (success, _, _) = await ExecutePowerShellAsync(cmd, PowerShellLane.Installer);
        return success;
    }

    public async Task<bool> RestorePointExistsAsync(string description)
    {
        string cmd =
            $"Get-ComputerRestorePoint | Where-Object {{ $_.Description -eq '{description}' }}";
        var (success, output, _) = await ExecutePowerShellAsync(cmd, PowerShellLane.General);
        return success && !string.IsNullOrWhiteSpace(output);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _installerSession?.Dispose();
        _installerSemaphore.Dispose();
        _poolSemaphore.Dispose();
        
        foreach (var session in _sessionPool)
        {
            session.Dispose();
        }
    }
}
