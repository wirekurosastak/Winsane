using System.Diagnostics;
using System.Management;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public sealed class SystemInfoService : IDisposable
{
    private const string RegSecureBoot = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    private const string RegHyperV = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization";
    private const string WmiTpmNamespace = @"root\cimv2\Security\MicrosoftTpm";
    private const string WmiTpmQuery = "SELECT * FROM Win32_Tpm";
    private const string WmiSecurityNamespace = @"root\SecurityCenter2";
    private const string WmiBitLockerNamespace = @"root\CIMV2\Security\MicrosoftVolumeEncryption";
    
    private const string RegDisplayVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string RegUac = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string RegDevMode = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock";

    private static readonly string[] GpuRegistryPaths =
    {
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000",
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001",
    };

    private float _totalRamGb = -1f;
    private bool _disposed;

    public SystemInfoService() { }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cpuCounter?.Dispose();
        if (_gpu3DCounters != null)
        {
            foreach (var counter in _gpu3DCounters)
            {
                counter.Dispose();
            }
        }
        _vramCounter?.Dispose();
        _ramCounter?.Dispose();

        _disposed = true;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo();

            try
            {
                GetHardwareInfo(info);
                GetOsInfo(info);
                GetSecurityInfo(info);
                GetExtraOsInfo(info);
                GetExtraSecurityInfo(info);
            }
            catch { }

            return info;
        });
    }

    private void GetHardwareInfo(SystemInfo info)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.MotherboardName = $"{obj["Manufacturer"]} {obj["Product"]}";
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.CpuName = obj["Name"]?.ToString()?.Trim() ?? "N/A";
                    var cores = obj["NumberOfCores"]?.ToString() ?? "0";
                    var threads = obj["NumberOfLogicalProcessors"]?.ToString() ?? "0";
                    info.CpuCores = $"{cores} Cores / {threads} Threads";
                    break;
                }
            }

            using (
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory")
            )
            {
                long totalRam = 0;
                int speed = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    totalRam += Convert.ToInt64(obj["Capacity"]);
                    speed = Convert.ToInt32(obj["Speed"]);
                }
                info.RamSpeed = $"{speed} MHz";
            }

            using (
                var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterRAM FROM Win32_VideoController"
                )
            )
            {
                foreach (var obj in searcher.Get())
                {
                    info.GpuName = obj["Name"]?.ToString() ?? "N/A";

                    var vramBytes = GetGpuVramFromRegistry();
                    if (vramBytes <= 0)
                    {
                        if (obj["AdapterRAM"] != null)
                            vramBytes = Convert.ToInt64(obj["AdapterRAM"]);
                    }

                    if (vramBytes > 0)
                    {
                        var vramGb = vramBytes / 1024.0 / 1024.0 / 1024.0;
                        info.GpuVramTotalGb = (float)vramGb;
                    }
                    break;
                }
            }
        }
        catch { }
        
        try
        {
             using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "N/A";
                    break;
                }
            }
        }
        catch { }
    }

    private void GetOsInfo(SystemInfo info)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_OperatingSystem"
            );
            foreach (var obj in searcher.Get())
            {
                info.OsCaption = obj["Caption"]?.ToString()?.Replace("Microsoft ", "")?.Trim() ?? "N/A";
                info.OsVersion = obj["Version"]?.ToString() ?? "N/A";
                info.OsArch = obj["OSArchitecture"]?.ToString() ?? "N/A";

                var installDate = obj["InstallDate"]?.ToString();
                if (!string.IsNullOrEmpty(installDate) && installDate.Length >= 8)
                {
                    var year = installDate.Substring(0, 4);
                    var month = installDate.Substring(4, 2);
                    var day = installDate.Substring(6, 2);
                    info.OsInstallDate = $"{year}-{month}-{day}";
                }

                var lastBoot = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(lastBoot) && lastBoot.Length >= 14)
                {
                    var year = lastBoot.Substring(0, 4);
                    var month = lastBoot.Substring(4, 2);
                    var day = lastBoot.Substring(6, 2);
                    var hour = lastBoot.Substring(8, 2);
                    var min = lastBoot.Substring(10, 2);

                    info.BootTime = $"{year}-{month}-{day} {hour}:{min}";
                }
                break;
            }
        }
        catch { }
    }

    private void GetSecurityInfo(SystemInfo info)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegSecureBoot))
            {
                var value = key?.GetValue("UEFISecureBootEnabled");
                info.SecureBootStatus = value?.ToString() == "1" ? "Enabled" : "Disabled";
            }

            using (var searcher = new ManagementObjectSearcher(WmiTpmNamespace, WmiTpmQuery))
            {
                foreach (var obj in searcher.Get())
                {
                    var activated = obj["IsActivated_InitialValue"]?.ToString() == "True";
                    var enabled = obj["IsEnabled_InitialValue"]?.ToString() == "True";
                    info.TpmStatus = enabled && activated ? "Enabled" : "Disabled";
                    break;
                }
            }

            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegHyperV))
            {
                info.HyperVStatus = key != null ? "Enabled" : "Disabled";
            }
        }
        catch { }
    }

    private PerformanceCounter? _cpuCounter;
    private List<PerformanceCounter>? _gpu3DCounters;
    private PerformanceCounter? _vramCounter;
    private PerformanceCounter? _ramCounter;
    private bool _countersInitialized;

    private void EnsureCountersInitialized()
    {
        if (_countersInitialized)
            return;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }
        catch
        {
            _cpuCounter = null;
        }

        try
        {
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch
        {
            _ramCounter = null;
        }

        InitializeGpuCounters();

        _countersInitialized = true;
    }

    private void InitializeGpuCounters()
    {
        try
        {
            // 1. Initialize 3D Engine Counters
            var categoryEngine = new PerformanceCounterCategory("GPU Engine");
            var instanceNamesEngine = categoryEngine.GetInstanceNames();

            _gpu3DCounters = new List<PerformanceCounter>();
            foreach (var instance in instanceNamesEngine)
            {
                if (!instance.Contains("engtype_3D"))
                    continue;

                // We try to capture all 3D engines to get a total "3D Load"
                var counter = new PerformanceCounter(
                    "GPU Engine",
                    "Utilization Percentage",
                    instance
                );
                counter.NextValue();
                _gpu3DCounters.Add(counter);
            }

            if (_gpu3DCounters.Count == 0)
                _gpu3DCounters = null;

            // 2. Initialize VRAM Counter (GPU Adapter Memory -> Dedicated Usage)
            // We'll simplisticly pick the first instance that looks like a physical adapter 
            // or the one with the highest usage if we could measure. 
            // For now, let's pick the first one that ends with _phys_0 (usually physical adapter 0)
            var categoryMem = new PerformanceCounterCategory("GPU Adapter Memory");
            var instanceNamesMem = categoryMem.GetInstanceNames();
            
            // Try to find one matching typical structure
            var targetInstance = instanceNamesMem.FirstOrDefault(x => x.Contains("_phys_0"));
            
            // Fallback to first if not found
            if (string.IsNullOrEmpty(targetInstance) && instanceNamesMem.Length > 0)
                targetInstance = instanceNamesMem[0];

            if (!string.IsNullOrEmpty(targetInstance))
            {
                _vramCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", targetInstance);
                _vramCounter.NextValue();
            }
        }
        catch
        {
            _gpu3DCounters = null;
            _vramCounter = null;
        }
    }

    public float GetCpuUsage()
    {
        EnsureCountersInitialized();
        try
        {
            return _cpuCounter?.NextValue() ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }

    public float GetGpu3DLoad()
    {
        EnsureCountersInitialized();
        try
        {
            if (_gpu3DCounters == null || _gpu3DCounters.Count == 0)
                return 0f;

            float totalUsage = 0f;
            foreach (var counter in _gpu3DCounters)
            {
                totalUsage += counter.NextValue();
            }

            // Cap at 100% just in case
            return Math.Min(totalUsage, 100f);
        }
        catch
        {
            return 0f;
        }
    }

    public float GetGpuVramUsageGb()
    {
        EnsureCountersInitialized();
        try
        {
            if (_vramCounter == null) return 0f;
            
            // Counter returns bytes
            var bytes = _vramCounter.NextValue();
            return (float)(bytes / 1024.0 / 1024.0 / 1024.0);
        }
        catch 
        { 
            return 0f;
        }
    }

    public (float UsedGb, float TotalGb, float Percentage) GetRamUsage()
    {
        EnsureCountersInitialized();

        if (_totalRamGb <= 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"
                );
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalBytes = Convert.ToDouble(obj["TotalVisibleMemorySize"]) * 1024;
                    _totalRamGb = (float)(totalBytes / 1024 / 1024 / 1024);
                    break;
                }
            }
            catch
            {
                return (0f, 0f, 0f);
            }
        }

        try
        {
            if (_ramCounter != null && _totalRamGb > 0)
            {
                var availableMb = _ramCounter.NextValue();
                var availableGb = availableMb / 1024f;
                var usedGb = _totalRamGb - availableGb;
                var percentage = (usedGb / _totalRamGb) * 100f;

                return (usedGb, _totalRamGb, percentage);
            }
        }
        catch { }

        return (0f, _totalRamGb > 0 ? _totalRamGb : 0f, 0f);
    }

    private static long GetGpuVramFromRegistry()
    {
        try
        {
            foreach (var path in GpuRegistryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                if (key == null)
                    continue;

                var qwMemSize = key.GetValue("HardwareInformation.qwMemorySize");
                if (qwMemSize != null)
                {
                    return Convert.ToInt64(qwMemSize);
                }

                var memSize = key.GetValue("HardwareInformation.MemorySize");
                if (memSize != null)
                {
                    return Convert.ToInt64(memSize);
                }
            }
        }
        catch { }

        return 0;
    }


    private void GetExtraOsInfo(SystemInfo info)
    {
        try
        {
            info.Hostname = Environment.MachineName;
            info.Username = Environment.UserName;
            info.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegDisplayVersion);
            info.DisplayVersion = key?.GetValue("DisplayVersion")?.ToString() ?? "N/A";
        }
        catch { }
    }

    private void GetExtraSecurityInfo(SystemInfo info)
    {
        try
        {
            // Antivirus
            using (var searcher = new ManagementObjectSearcher(WmiSecurityNamespace, "SELECT * FROM AntivirusProduct"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.AntivirusStatus = obj["displayName"]?.ToString() ?? "Unknown";
                    break;
                }
                if (info.AntivirusStatus == "N/A") info.AntivirusStatus = "None";
            }

            
            // UAC
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegUac))
            {
                 var val = key?.GetValue("EnableLUA");
                 info.UacStatus = val?.ToString() == "1" ? "Enabled" : "Disabled";
            }

            // Developer Mode
             using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegDevMode))
            {
                 var val = key?.GetValue("AllowDevelopmentWithoutDevLicense");
                 info.DeveloperModeStatus = val?.ToString() == "1" ? "Enabled" : "Disabled";
            }

            // BitLocker
            try
            {
                using (var searcher = new ManagementObjectSearcher(WmiBitLockerNamespace, "SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var status = obj["ProtectionStatus"]?.ToString();
                        info.BitLockerStatus = status == "1" ? "Enabled" : "Disabled";
                        break;
                    }
                }
            } catch {}
        }
        catch { }
    }
}
