namespace Winsane.Core.Models;

public class SystemInfo
{
    public string MotherboardName { get; set; } = "N/A";
    public string CpuName { get; set; } = "N/A";
    public string CpuCores { get; set; } = "N/A";
    public string RamSpeed { get; set; } = "N/A";
    public string GpuName { get; set; } = "N/A";
    public float GpuUtilization3D { get; set; }
    public float GpuVramUsedGb { get; set; }
    public float GpuVramTotalGb { get; set; }
    
    // OS Info
    public string OsCaption { get; set; } = "N/A";
    public string OsVersion { get; set; } = "N/A";
    public string OsArch { get; set; } = "N/A";
    public string OsInstallDate { get; set; } = "N/A";
    public string BootTime { get; set; } = "N/A";
    public string Hostname { get; set; } = "N/A";
    public string Username { get; set; } = "N/A";
    public string DisplayVersion { get; set; } = "N/A";
    public string BiosVersion { get; set; } = "N/A";
    public TimeSpan Uptime { get; set; }

    // Security Info
    public string SecureBootStatus { get; set; } = "N/A";
    public string TpmStatus { get; set; } = "N/A";
    public string HyperVStatus { get; set; } = "N/A";
    public string AntivirusStatus { get; set; } = "N/A";
    public string UacStatus { get; set; } = "N/A";
    public string DeveloperModeStatus { get; set; } = "N/A";
    public string BitLockerStatus { get; set; } = "N/A";
}
