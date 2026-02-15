using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;
using CommunityToolkit.Mvvm.Input;

namespace Winsane.UI.ViewModels;

public partial class SystemViewModel : ViewModelBase
{
    [ObservableProperty] private string _motherboardName = "N/A";
    [ObservableProperty] private string _cpuName = "N/A";
    [ObservableProperty] private string _cpuCores = "N/A";
    [ObservableProperty] private string _cpuUsage = "N/A";
    [ObservableProperty] private string _ramSpeed = "N/A";
    [ObservableProperty] private string _ramUsage = "N/A";
    [ObservableProperty] private string _gpuName = "N/A";
    [ObservableProperty] private string _gpu3DLoad = "N/A";
    [ObservableProperty] private string _gpuVramUsage = "N/A";
    [ObservableProperty] private string _osCaption = "N/A";
    [ObservableProperty] private string _osVersion = "N/A";
    [ObservableProperty] private string _osArch = "N/A";
    [ObservableProperty] private string _osInstallDate = "N/A";
    [ObservableProperty] private string _bootTime = "N/A";
    [ObservableProperty] private string _secureBootStatus = "N/A";
    [ObservableProperty] private string _tpmStatus = "N/A";
    [ObservableProperty] private string _hyperVStatus = "N/A";
    [ObservableProperty] private string _hostname = "N/A";
    [ObservableProperty] private string _username = "N/A";
    [ObservableProperty] private string _displayVersion = "N/A";
    [ObservableProperty] private string _biosVersion = "N/A";
    [ObservableProperty] private string _uptime = "N/A";
    [ObservableProperty] private string _antivirusStatus = "N/A";
    [ObservableProperty] private string _uacStatus = "N/A";
    [ObservableProperty] private string _developerModeStatus = "N/A";
    [ObservableProperty] private string _bitLockerStatus = "N/A";

    private float _gpuVramTotalGb;
    private Avalonia.Threading.DispatcherTimer? _refreshTimer;
    private SystemInfoService? _systemInfoService;

    // --- Power Timer ---
    [ObservableProperty] private int _hours;
    [ObservableProperty] private int _minutes;
    [ObservableProperty] private int _seconds;
    [ObservableProperty] private string _remainingTimeMessage = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShutdownCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    [NotifyCanExecuteChangedFor(nameof(BiosCommand))]
    [NotifyCanExecuteChangedFor(nameof(HibernateCommand))]
    [NotifyCanExecuteChangedFor(nameof(SleepCommand))]
    private bool _isTimerRunning;

    public bool CanStartAction => !IsTimerRunning;

    public void Refresh(SystemInfoService systemInfoService)
    {
        _systemInfoService = systemInfoService;
        LoadSystemInfoAsync();
        if (_refreshTimer == null)
        {
            _refreshTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (_, _) => RefreshDynamicValues();
        }
        _refreshTimer.Start();
    }

    private async void LoadSystemInfoAsync()
    {
        if (_systemInfoService == null) return;
        try
        {
            var i = await _systemInfoService.GetSystemInfoAsync();
            MotherboardName = i.MotherboardName; CpuName = i.CpuName; CpuCores = i.CpuCores;
            RamSpeed = i.RamSpeed; GpuName = i.GpuName; _gpuVramTotalGb = i.GpuVramTotalGb;
            OsCaption = i.OsCaption; OsVersion = i.OsVersion; OsArch = i.OsArch;
            OsInstallDate = i.OsInstallDate; BootTime = i.BootTime;
            SecureBootStatus = i.SecureBootStatus; TpmStatus = i.TpmStatus; HyperVStatus = i.HyperVStatus;
            Hostname = i.Hostname; Username = i.Username; DisplayVersion = i.DisplayVersion;
            BiosVersion = i.BiosVersion;
            AntivirusStatus = i.AntivirusStatus; UacStatus = i.UacStatus;
            DeveloperModeStatus = i.DeveloperModeStatus; BitLockerStatus = i.BitLockerStatus;
        }
        catch { }
    }

    private void RefreshDynamicValues()
    {
        if (_systemInfoService == null) return;
        try
        {
            CpuUsage = $"{_systemInfoService.GetCpuUsage():F1}%";
            var (usedGb, totalGb, pct) = _systemInfoService.GetRamUsage();
            RamUsage = $"{pct:F1}% ({usedGb:F1} GB / {totalGb:F1} GB)";
            Gpu3DLoad = $"{_systemInfoService.GetGpu3DLoad():F1}%";
            var vramUsed = _systemInfoService.GetGpuVramUsageGb();
            GpuVramUsage = _gpuVramTotalGb > 0
                ? $"{(vramUsed / _gpuVramTotalGb) * 100f:F1}% ({vramUsed:F1} GB / {_gpuVramTotalGb:F0} GB)"
                : $"{vramUsed:F1} GB";
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            Uptime = $"{up.Days}d {up.Hours}h {up.Minutes}m {up.Seconds}s";
        }
        catch { }
    }

    public void StopRefresh() => _refreshTimer?.Stop();

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Shutdown() => StartTimerAndExecute("Shutting down", () => RunProcess("shutdown", "/s /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Restart() => StartTimerAndExecute("Restarting", () => RunProcess("shutdown", "/r /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Bios() => StartTimerAndExecute("Restarting to BIOS", () => RunProcess("shutdown", "/r /fw /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Hibernate() => StartTimerAndExecute("Hibernating", () => RunProcess("shutdown", "/h"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Sleep() => StartTimerAndExecute("Going to Sleep", () =>
    {
        SetSuspendState(false, true, false);
        return Task.CompletedTask;
    });

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        IsTimerRunning = false;
        StatusMessage = "Cancelled";
    }

    private async void StartTimerAndExecute(string actionName, Func<Task> powerAction)
    {
        long totalSeconds = (Hours * 3600) + (Minutes * 60) + Seconds;
        if (totalSeconds < 0) totalSeconds = 0;

        StatusMessage = actionName;
        IsTimerRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            if (totalSeconds > 0)
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                var t = TimeSpan.FromSeconds(totalSeconds);
                RemainingTimeMessage = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

                while (totalSeconds > 0 && await timer.WaitForNextTickAsync(token))
                {
                    t = TimeSpan.FromSeconds(--totalSeconds);
                    RemainingTimeMessage = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
                }
            }
            if (!token.IsCancellationRequested)
            {
                RemainingTimeMessage = "Now";
                await powerAction();
            }
        }
        catch (OperationCanceledException) { }
        finally { IsTimerRunning = false; }
    }

    private Task RunProcess(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName, Arguments = arguments,
                CreateNoWindow = true, UseShellExecute = false
            });
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); }
        return Task.CompletedTask;
    }

    [System.Runtime.InteropServices.DllImport("PowrProf.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
