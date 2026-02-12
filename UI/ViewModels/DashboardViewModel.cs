using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _motherboardName = "N/A";

    [ObservableProperty]
    private string _cpuName = "N/A";

    [ObservableProperty]
    private string _cpuCores = "N/A";

    [ObservableProperty]
    private string _cpuUsage = "N/A";

    [ObservableProperty]
    private string _ramSpeed = "N/A";

    [ObservableProperty]
    private string _ramUsage = "N/A";

    [ObservableProperty]
    private string _gpuName = "N/A";

    [ObservableProperty]
    private string _gpuMemory = "N/A";

    [ObservableProperty]
    private string _gpuUsage = "N/A";

    [ObservableProperty]
    private string _osCaption = "N/A";

    [ObservableProperty]
    private string _osVersion = "N/A";

    [ObservableProperty]
    private string _osArch = "N/A";

    [ObservableProperty]
    private string _osInstallDate = "N/A";

    [ObservableProperty]
    private string _bootTime = "N/A";

    [ObservableProperty]
    private string _secureBootStatus = "N/A";

    [ObservableProperty]
    private string _tpmStatus = "N/A";

    [ObservableProperty]
    private string _hyperVStatus = "N/A";

    private Avalonia.Threading.DispatcherTimer? _refreshTimer;
    private SystemInfoService? _systemInfoService;

    public void Refresh(SystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;

        LoadSystemInfoAsync();

        if (_refreshTimer == null)
        {
            _refreshTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
            };
            _refreshTimer.Tick += (s, e) => RefreshDynamicValues();
        }
        _refreshTimer.Start();
    }

    private async void LoadSystemInfoAsync()
    {
        if (_systemInfoService == null)
            return;

        try
        {
            var info = await _systemInfoService.GetSystemInfoAsync();

            MotherboardName = info.MotherboardName;
            CpuName = info.CpuName;
            CpuCores = info.CpuCores;
            RamSpeed = info.RamSpeed;
            GpuName = info.GpuName;
            GpuMemory = info.GpuMemory;
            OsCaption = info.OsCaption;
            OsVersion = info.OsVersion;
            OsArch = info.OsArch;
            OsInstallDate = info.OsInstallDate;
            BootTime = info.BootTime;
            SecureBootStatus = info.SecureBootStatus;
            TpmStatus = info.TpmStatus;
            HyperVStatus = info.HyperVStatus;
        }
        catch { }
    }

    private void RefreshDynamicValues()
    {
        if (_systemInfoService == null)
            return;

        try
        {
            var cpu = _systemInfoService.GetCpuUsage();
            CpuUsage = $"{cpu:F1}%";

            var (usedGb, totalGb, percentage) = _systemInfoService.GetRamUsage();
            RamUsage = $"{percentage:F1}% ({usedGb:F1} GB / {totalGb:F1} GB)";

            var gpuUsage = _systemInfoService.GetGpuUsage();
            GpuUsage = gpuUsage >= 0 ? $"{gpuUsage:F1}%" : "N/A";
        }
        catch { }
    }

    public void StopRefresh()
    {
        _refreshTimer?.Stop();
    }
}
