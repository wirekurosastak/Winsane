namespace Winsane.Core.Models;

public class SystemInfo
{
    public string MotherboardName { get; set; } = "N/A";
    public string CpuName { get; set; } = "N/A";
    public string CpuCores { get; set; } = "N/A";
    public string RamSpeed { get; set; } = "N/A";
    public string GpuName { get; set; } = "N/A";
    public string GpuMemory { get; set; } = "N/A";
    public string OsCaption { get; set; } = "N/A";
    public string OsVersion { get; set; } = "N/A";
    public string OsArch { get; set; } = "N/A";
    public string OsInstallDate { get; set; } = "N/A";
    public string BootTime { get; set; } = "N/A";
    public string SecureBootStatus { get; set; } = "N/A";
    public string TpmStatus { get; set; } = "N/A";
    public string HyperVStatus { get; set; } = "N/A";
}
