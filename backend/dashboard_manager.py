import platform
import psutil
import datetime
from collections import Counter
import winreg

# --- Optional Imports ---
try:
    import GPUtil
    GPUTIL_AVAILABLE = True
except ImportError:
    GPUTIL_AVAILABLE = False

try:
    import wmi
    WMI_AVAILABLE = True
except ImportError:
    WMI_AVAILABLE = False

class SystemInfoManager:
    def __init__(self):
        global WMI_AVAILABLE
        
        self.w = None
        self.w_storage = None 

        # Attempt to initialize WMI
        if WMI_AVAILABLE:
            try:
                self.w = wmi.WMI()
                self.w_storage = wmi.WMI(namespace="root/Microsoft/Windows/Storage")
            except Exception as e:
                print(f"WMI error: {e}")
                if not self.w: WMI_AVAILABLE = False
                self.w_storage = None 
                
        # Fetch static info (like CPU name, OS)
        self.static_info = self._get_static_info()

    def _format_bytes(self, b):
        # bytes to GB
        gb = b / (1024**3)
        return f"{gb:.1f} GB"
    
    def _safe_wmi_query(self, query_func, default_value):
        # Wrapper to safely execute WMI queries, providing a fallback
        if not WMI_AVAILABLE or not self.w:
            return default_value
        try:
            return query_func()
        except Exception as e:
            print(f"WMI query error: {e}")
            return default_value

    def _get_static_info(self):
        # Gathers all information that does not change (run once)
        info = {}
        
        # OS Info
        def os_query():
            os_info = self.w.Win32_OperatingSystem()[0]
            install_date_str = os_info.InstallDate.split('.')[0]
            install_date_obj = datetime.datetime.strptime(install_date_str, '%Y%m%d%H%M%S')
            install_date = install_date_obj.strftime('%Y-%m-%d')
            
            return {
                "os_caption": os_info.Caption.strip(),
                "os_version": os_info.Version,
                "os_arch": os_info.OSArchitecture,
                "os_install_date": install_date
            }
        
        fallback_os = {
            "os_caption": f"{platform.system()} {platform.release()}",
            "os_version": platform.version(),
            "os_arch": platform.machine(),
            "os_install_date": "N/A"
        }
        info.update(self._safe_wmi_query(os_query, default_value=fallback_os))

        # Boot Time
        try:
            boot_time = datetime.datetime.fromtimestamp(psutil.boot_time())
            info["boot_time"] = boot_time.strftime("%Y-%m-%d %H:%M:%S")
        except Exception: info["boot_time"] = "N/A"

        # Motherboard Info
        def mb_query():
            board = self.w.Win32_BaseBoard()[0]
            return {"mb_manufacturer": board.Manufacturer.strip(), "mb_product": board.Product.strip()}
        info.update(self._safe_wmi_query(mb_query, 
                    default_value={"mb_manufacturer": "N/A", "mb_product": "N/A"}))

        # CPU Info
        def cpu_query():
            cpu_wmi = self.w.Win32_Processor()[0]
            return {"cpu": cpu_wmi.Name.strip(), "cpu_cores": cpu_wmi.NumberOfCores, "cpu_threads": cpu_wmi.NumberOfLogicalProcessors}
        info.update(self._safe_wmi_query(cpu_query, 
                    default_value={"cpu": platform.processor(), "cpu_cores": "N/A", "cpu_threads": "N/A"}))

        # RAM Info
        def ram_query():
            ram_chips = self.w.Win32_PhysicalMemory()
            if not ram_chips: raise Exception("No RAM info")
            first_chip = ram_chips[0]
            
            return {"ram_speed": f"{first_chip.Speed} MHz", 
                    "ram_slots_used": len(ram_chips)}
        
        info.update(self._safe_wmi_query(ram_query, 
                    default_value={"ram_speed": "N/A", "ram_slots_used": "N/A"}))
        
        # Secure Boot
        try:
            key = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\SecureBoot\State")
            value, _ = winreg.QueryValueEx(key, "UEFISecureBootEnabled")
            info["secure_boot_status"] = "Enabled" if value == 1 else "Disabled"
            winreg.CloseKey(key)
        except (OSError, FileNotFoundError):
            info["secure_boot_status"] = "N/A (Check BIOS)"
        
        # TPM
        def tpm_query():
            # Connect to the specific namespace for TPM
            w_tpm = wmi.WMI(namespace="root/CIMV2/Security/MicrosoftTpm")
            tpm_info = w_tpm.Win32_Tpm()
            if not tpm_info:
                return {"tpm_status": "Not Found"}
            tpm = tpm_info[0]
            
            spec_version = tpm.SpecVersion.split(',')[0].strip() if tpm.SpecVersion else "N/A"
            status = "N/A"
            
            if tpm.IsActivated_InitialValue and tpm.IsEnabled_InitialValue:
                status = f"Enabled & Activated (v{spec_version})"
            elif tpm.IsEnabled_InitialValue:
                status = f"Enabled (v{spec_version})"
            else:
                status = f"Disabled (v{spec_version})"
            
            return {"tpm_status": status}
        
        # We must use a separate wrapper for this query, as it uses a different namespace
        if WMI_AVAILABLE:
            try:
                info.update(tpm_query())
            except Exception as e:
                print(f"TPM WMI query error: {e}")
                info.update({"tpm_status": "N/A (Query Failed)"})
        else:
            info.update({"tpm_status": "N/A (WMI Error)"})


        # Hyper-V
        def hyperv_query():
            cs_info = self.w.Win32_ComputerSystem()[0]
            # HypervisorPresent is a boolean, use getattr for safety
            return {"hyperv_status": "Enabled" if getattr(cs_info, 'HypervisorPresent', False) else "Disabled"}
        info.update(self._safe_wmi_query(hyperv_query, default_value={"hyperv_status": "N/A"}))
        
        # Get list of drive partitions (C:\, D:\) for dynamic updates later
        info['drives'] = []
        try:
            partitions = psutil.disk_partitions()
            for p in partitions:
                if 'rw' in p.opts and p.fstype:
                    try:
                        usage = psutil.disk_usage(p.mountpoint)
                        info['drives'].append({
                            "mountpoint": p.mountpoint,
                            "total_str": self._format_bytes(usage.total)
                        })
                    except (PermissionError, FileNotFoundError): pass
        except Exception as e:
             print(f"psutil disk_partitions error: {e}")

        return info

    def get_static_data(self):
        # Public getter for the static data
        return self.static_info

    def get_dynamic_data(self):
        # Gathers all information that changes frequently (CPU load, RAM usage)
        info = {}
        
        # CPU Load
        info["cpu_load"] = f"{psutil.cpu_percent():.1f}%"
        
        # RAM Usage
        mem = psutil.virtual_memory()
        info["ram_percent"] = f"{mem.percent:.1f}"
        info["ram_used"] = self._format_bytes(mem.used)
        info["ram_total"] = self._format_bytes(mem.total)

        # Disk Partition Usage
        info['disk_usages'] = []
        for drive in self.static_info.get('drives', []):
            try:
                mount = drive['mountpoint']
                usage = psutil.disk_usage(mount)
                
                info['disk_usages'].append({
                    "mountpoint": mount,
                    "percent": f"{usage.percent:.1f}",
                    "used_str": self._format_bytes(usage.used)
                })
            except Exception:
                pass # Ignore errors (e.g., drive disconnected)

        # GPU Info (NVIDIA only, via GPUtil)
        if GPUTIL_AVAILABLE:
            try:
                gpus = GPUtil.getGPUs()
                if gpus:
                    gpu = gpus[0]
                    info["gpu_name"] = gpu.name
                    info["gpu_load"] = f"{gpu.load * 100:.1f}%"
                    info["gpu_mem_used"] = f"{gpu.memoryUsed / 1024:.1f} GB"
                    info["gpu_mem_total"] = f"{gpu.memoryTotal / 1024:.1f} GB"
                else: raise Exception("No GPU found by GPUtil")
            except Exception:
                info["gpu_name"] = "N/A (NVIDIA only)"
                info["gpu_load"] = "N/A"
                info["gpu_mem_used"] = "N/A"
                info["gpu_mem_total"] = "N/A"
        else:
            info["gpu_name"] = "N/A (GPUtil not installed)"
            info["gpu_load"] = "N/A"
            info["gpu_mem_used"] = "N/A"
            info["gpu_mem_total"] = "N/A"

        return info