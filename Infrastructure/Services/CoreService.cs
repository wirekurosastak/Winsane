using System.Collections.Concurrent;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

/// <summary>
/// Unified Core Service handling System operations.
/// Uses a pool of PowerShell sessions to execute commands efficiently.
/// </summary>
public class CoreService : IDisposable
{
    // Pool Configuration
    private const int MaxPoolSize = 3; // Limit to 3 concurrent PowerShell processes
    private readonly ConcurrentQueue<PowerShellSession> _sessionPool = new();
    private readonly SemaphoreSlim _poolSemaphore;

    public CoreService()
    {
        _poolSemaphore = new SemaphoreSlim(MaxPoolSize, MaxPoolSize);
        
        // Pre-warm the pool
        for (int i = 0; i < MaxPoolSize; i++)
        {
            _sessionPool.Enqueue(new PowerShellSession());
        }
    }

    /// <summary>
    /// Executes a PowerShell command using a session from the pool.
    /// Thread-safe and limits concurrent processes.
    /// </summary>
    public async Task<(bool Success, string Output, string Error)> ExecutePowerShellAsync(string command)
    {
        PowerShellSession? session = null;
        
        // Wait for an available slot in the pool
        await _poolSemaphore.WaitAsync();
        
        try
        {
            // Try to get a session
            if (!_sessionPool.TryDequeue(out session))
            {
                // Should not happen due to semaphore, but safety fallback
                session = new PowerShellSession();
            }
            
            return await session.ExecuteCommandAsync(command);
        }
        finally
        {
            if (session != null)
            {
                // Return session to pool
                _sessionPool.Enqueue(session);
            }
            _poolSemaphore.Release();
        }
    }

    public async Task<bool> CreateSystemRestorePointAsync(string description)
    {
        string cmd = $"Checkpoint-Computer -Description \"{description}\" -RestorePointType \"MODIFY_SETTINGS\"";
        var (success, _, _) = await ExecutePowerShellAsync(cmd);
        return success;
    }
    
    public void Dispose()
    {
        while (_sessionPool.TryDequeue(out var session))
        {
            session.Dispose();
        }
        _poolSemaphore.Dispose();
    }
}