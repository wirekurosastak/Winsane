namespace Winsane.Core.Models;

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
