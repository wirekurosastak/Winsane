using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Threading;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;
using CommunityToolkit.Mvvm.Input;

namespace Winsane.UI.ViewModels;

public partial class SystemViewModel : ViewModelBase
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
    // --- Power Timer Logic ---

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

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Shutdown() => StartTimerAndExecute("Shutting down", async () => await RunProcess("shutdown", "/s /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Restart() => StartTimerAndExecute("Restarting", async () => await RunProcess("shutdown", "/r /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Bios() => StartTimerAndExecute("Restarting to BIOS", async () => await RunProcess("shutdown", "/r /fw /t 0"));

    [RelayCommand(CanExecute = nameof(CanStartAction))]
    private void Hibernate() => StartTimerAndExecute("Hibernating", async () => await RunProcess("shutdown", "/h"));

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
                
                TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
                RemainingTimeMessage = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

                while (totalSeconds > 0 && await timer.WaitForNextTickAsync(token))
                {
                    totalSeconds--;
                    t = TimeSpan.FromSeconds(totalSeconds);
                    RemainingTimeMessage = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
                }
            }

            if (!token.IsCancellationRequested)
            {
                RemainingTimeMessage = "Now";
                await powerAction();
            }
        }
        catch (OperationCanceledException)
        {
            // Ignored
        }
        finally
        {
            IsTimerRunning = false;
        }
    }

    private Task RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
        return Task.CompletedTask;
    }

    [System.Runtime.InteropServices.DllImport("PowrProf.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}
