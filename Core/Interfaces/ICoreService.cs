using Winsane.Core.Models;

namespace Winsane.Core.Interfaces;

public interface ICoreService
{
    // PowerShell
    Task<(bool Success, string Output, string Error)> ExecutePowerShellAsync(string command);
    Task<bool> ExecutePowerShellAsAdminAsync(string command);

    // Winget
    Task<bool> QueueWingetTaskAsync(string packageId, bool install, string displayName);
    Task<bool> IsWingetInstalledAsync(string packageId);
    event EventHandler<WingetProgressEventArgs>? WingetProgressChanged;
    event EventHandler<WingetCompletedEventArgs>? WingetTaskCompleted;

    // Backup
    Task<bool> CreateSystemRestorePointAsync(string description);
}

// Support classes moved to Core or kept near interface if simple DTOs? 
// They are used in events so they should be accessible. 
// I will keep them here or in Models. For now, I'll rely on namespace resolution 
// but wait, WingetProgressEventArgs were defined in CoreService.cs. 
// I should move those EventArg classes to Core/Models/Events.cs or similar.
// For now I will assume they are in Models namespace if I move them there.
