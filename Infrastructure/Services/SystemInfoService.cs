using System.Diagnostics;
using System.Management;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public sealed class SystemInfoService : IDisposable
{
    // Registry Constants
    private const string RegSecureBoot = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    private const string RegHyperV = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization";
    private const string WmiTpmNamespace = @"root\cimv2\Security\MicrosoftTpm";
    private const string WmiTpmQuery = "SELECT * FROM Win32_Tpm";
    
    // GPU Registry Locations
    private static readonly string[] GpuRegistryPaths =
    {
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000",
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001",
    };

    private float _totalRamGb = -1f;
    private bool _disposed;
    
    public SystemInfoService()
    {
        // Defer heavy initialization to first usage or background to prevent startup blocking
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _cpuCounter?.Dispose();
        if (_gpuCounters != null)
        {
            foreach (var counter in _gpuCounters)
            {
                counter.Dispose();
            }
        }
        _ramCounter?.Dispose();
        
        _disposed = true;
    }
    
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo();
            
            GetHardwareInfo(info);
            GetOsInfo(info);
            GetSecurityInfo(info);
            
            return info;
        });
    }

    private void GetHardwareInfo(SystemInfo info)
    {
        try
        {
            // Motherboard
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    info.MotherboardName = $"{obj["Manufacturer"]} {obj["Product"]}";
                    break;
                }
            }
    
            // CPU
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
    
            // RAM
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
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
    
            // GPU
            using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
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
                        info.GpuMemory = vramGb >= 1 ? $"{vramGb:F0} GB" : $"{vramBytes / 1024 / 1024} MB";
                    }
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
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                info.OsCaption = obj["Caption"]?.ToString() ?? "N/A";
                info.OsVersion = obj["Version"]?.ToString() ?? "N/A";
                info.OsArch = obj["OSArchitecture"]?.ToString() ?? "N/A";
                
                // Install Date
                var installDate = obj["InstallDate"]?.ToString();
                if (!string.IsNullOrEmpty(installDate) && installDate.Length >= 8)
                {
                    var year = installDate.Substring(0, 4);
                    var month = installDate.Substring(4, 2);
                    var day = installDate.Substring(6, 2);
                    info.OsInstallDate = $"{year}-{month}-{day}";
                }
                
                // Boot Time
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
            // Secure Boot
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegSecureBoot))
            {
                var value = key?.GetValue("UEFISecureBootEnabled");
                info.SecureBootStatus = value?.ToString() == "1" ? "Enabled" : "Disabled";
            }
            
            // TPM
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
            
            // Hyper-V
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegHyperV))
            {
                info.HyperVStatus = key != null ? "Enabled" : "Disabled";
            }
        }
        catch { }
    }
    
    // --- Performance Counters ---
    // Metrics are lazy-loaded to avoid blocking application startup.
    
    private PerformanceCounter? _cpuCounter;
    private List<PerformanceCounter>? _gpuCounters;
    private PerformanceCounter? _ramCounter;
    private bool _countersInitialized;
    
    private void EnsureCountersInitialized()
    {
        if (_countersInitialized) return;
        
        // CPU
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call always returns 0
        }
        catch { _cpuCounter = null; }

        // RAM
        try
        {
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch { _ramCounter = null; }
        
        // GPU (Find all 3D/Graphics engines)
        InitializeGpuCounters();
        
        _countersInitialized = true;
    }
    
    private void InitializeGpuCounters()
    {
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            
            // Collect all eng_0 (main 3D engine) instances across all GPUs
            // This matches Task Manager's approach of summing across all adapters
            // Instance format: pid_XXXX_luid_0x00000000_0xYYYYYYYY_phys_0_eng_0_engtype_3D
            _gpuCounters = new List<PerformanceCounter>();
            foreach (var instance in instances)
            {
                // Only include 3D/Graphics engines
                if (!instance.Contains("engtype_3D") && !instance.Contains("engtype_Graphics"))
                    continue;
                
                // Only use eng_0 (primary 3D engine) to avoid double-counting
                // Each GPU has multiple engines (eng_0 through eng_N), but
                // eng_0 is the main 3D engine that handles most rendering
                if (!instance.Contains("_eng_0_"))
                    continue;
                    
                var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                counter.NextValue(); // First call always returns 0
                _gpuCounters.Add(counter);
            }
            
            if (_gpuCounters.Count == 0)
            {
                _gpuCounters = null;
            }
        }
        catch { _gpuCounters = null; }
    }

    public float GetCpuUsage()
    {
        EnsureCountersInitialized();
        try
        {
            return _cpuCounter?.NextValue() ?? 0f;
        }
        catch { return 0f; }
    }
    
    public float GetGpuUsage()
    {
        EnsureCountersInitialized();
        try
        {
            if (_gpuCounters == null || _gpuCounters.Count == 0)
                return -1f;
            
            // Sum all GPU engine utilization values
            float totalUsage = 0f;
            foreach (var counter in _gpuCounters)
            {
                totalUsage += counter.NextValue();
            }
            
            // Cap at 100% (sum can exceed 100 if multiple engines are active)
            return Math.Min(totalUsage, 100f);
        }
        catch { return -1f; }
    }




    public (float UsedGb, float TotalGb, float Percentage) GetRamUsage()
    {
        EnsureCountersInitialized();
        
        // Ensure we have total RAM
        if (_totalRamGb <= 0)
        {
             try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalBytes = Convert.ToDouble(obj["TotalVisibleMemorySize"]) * 1024;
                    _totalRamGb = (float)(totalBytes / 1024 / 1024 / 1024);
                    break;
                }
            }
            catch { return (0f, 0f, 0f); }
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
    
    /// <summary>
    /// Get GPU VRAM from registry (works for GPUs > 4GB)
    /// </summary>
    private static long GetGpuVramFromRegistry()
    {
        try
        {
            foreach (var path in GpuRegistryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;
                
                // Try qwMemorySize (QWORD - 64-bit, used by modern drivers)
                var qwMemSize = key.GetValue("HardwareInformation.qwMemorySize");
                if (qwMemSize != null)
                {
                    return Convert.ToInt64(qwMemSize);
                }
                
                // Try MemorySize (DWORD - 32-bit legacy)
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
}

