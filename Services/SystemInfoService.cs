using System.Diagnostics;
using System.Management;

namespace WinsaneCS.Services;

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

public class SystemInfoService
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _gpuCounter;
    
    public SystemInfoService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call returns 0
        }
        catch
        {
            _cpuCounter = null;
        }
        
        // Try to initialize GPU counter (Windows 10+ GPU Engine counters)
        InitializeGpuCounter();
    }
    
    private void InitializeGpuCounter()
    {
        try
        {
            // Windows 10+ exposes GPU usage via "GPU Engine" category
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            
            // Find the 3D engine (primary GPU rendering)
            foreach (var instance in instances)
            {
                if (instance.Contains("engtype_3D") || instance.Contains("engtype_Graphics"))
                {
                    _gpuCounter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance);
                    _gpuCounter.NextValue(); // First call returns 0
                    return;
                }
            }
        }
        catch
        {
            _gpuCounter = null;
        }
    }
    
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo();
            
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
                
                // GPU - Use multiple sources for VRAM since AdapterRAM is 32-bit limited
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        info.GpuName = obj["Name"]?.ToString() ?? "N/A";
                        
                        // AdapterRAM is UInt32, maxes at ~4GB
                        // Try registry for actual VRAM on modern GPUs
                        var vramBytes = GetGpuVramFromRegistry();
                        if (vramBytes <= 0)
                        {
                            // Fallback to WMI (limited to 4GB)
                            vramBytes = Convert.ToInt64(obj["AdapterRAM"] ?? 0);
                        }
                        
                        if (vramBytes > 0)
                        {
                            var vramGb = vramBytes / 1024.0 / 1024.0 / 1024.0;
                            info.GpuMemory = vramGb >= 1 ? $"{vramGb:F0} GB" : $"{vramBytes / 1024 / 1024} MB";
                        }
                        break;
                    }
                }
                
                // OS
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
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
                        
                        // Boot Time - Show actual date/time
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
                
                // Security - Secure Boot
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                    var value = key?.GetValue("UEFISecureBootEnabled");
                    info.SecureBootStatus = value?.ToString() == "1" ? "Enabled" : "Disabled";
                }
                catch
                {
                    info.SecureBootStatus = "Unknown";
                }
                
                // Security - TPM
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2\Security\MicrosoftTpm", 
                        "SELECT * FROM Win32_Tpm");
                    foreach (var obj in searcher.Get())
                    {
                        var activated = obj["IsActivated_InitialValue"]?.ToString() == "True";
                        var enabled = obj["IsEnabled_InitialValue"]?.ToString() == "True";
                        info.TpmStatus = enabled && activated ? "Enabled" : "Disabled";
                        break;
                    }
                }
                catch
                {
                    info.TpmStatus = "Not Found";
                }
                
                // Security - Hyper-V
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization");
                    info.HyperVStatus = key != null ? "Enabled" : "Disabled";
                }
                catch
                {
                    info.HyperVStatus = "Unknown";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system info: {ex.Message}");
            }
            
            return info;
        });
    }
    
    public float GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }
    
    public float GetGpuUsage()
    {
        try
        {
            return _gpuCounter?.NextValue() ?? -1f; // -1 means not available
        }
        catch
        {
            return -1f;
        }
    }
    
    public (float UsedGb, float TotalGb, float Percentage) GetRamUsage()
    {
        try
        {
            var gcMemInfo = GC.GetGCMemoryInfo();
            var totalMemory = gcMemInfo.TotalAvailableMemoryBytes;
            
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalVisible = Convert.ToDouble(obj["TotalVisibleMemorySize"]) * 1024; // KB to bytes
                var freePhysical = Convert.ToDouble(obj["FreePhysicalMemory"]) * 1024;
                
                var usedBytes = totalVisible - freePhysical;
                var usedGb = (float)(usedBytes / 1024 / 1024 / 1024);
                var totalGb = (float)(totalVisible / 1024 / 1024 / 1024);
                var percentage = (float)(usedBytes / totalVisible * 100);
                
                return (usedGb, totalGb, percentage);
            }
        }
        catch { }
        
        return (0f, 0f, 0f);
    }
    
    /// <summary>
    /// Get GPU VRAM from registry (works for GPUs > 4GB)
    /// </summary>
    private static long GetGpuVramFromRegistry()
    {
        try
        {
            // Check AMD/Intel/NVIDIA registry locations for dedicated video memory
            string[] registryPaths = new[]
            {
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000",
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001",
            };
            
            foreach (var path in registryPaths)
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

