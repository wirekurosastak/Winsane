using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winsane.UI.ViewModels;

public partial class PowerViewModel : ViewModelBase
{
    [ObservableProperty] private int _hours;
    [ObservableProperty] private int _minutes;
    [ObservableProperty] private int _seconds;
    
    [ObservableProperty] private string _remainingTimeMessage = string.Empty;
    
    // New property to hold the specific action name (e.g. "Shutting down")
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

    // --- Commands Updated with Action Names ---

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

    // --- Core Logic Updated ---

    private async void StartTimerAndExecute(string actionName, Func<Task> powerAction)
    {
        long totalSeconds = (Hours * 3600) + (Minutes * 60) + Seconds;
        if (totalSeconds < 0) totalSeconds = 0;

        // Set the status message immediately
        StatusMessage = actionName;
        IsTimerRunning = true;
        
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            if (totalSeconds > 0)
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                
                // Initial update before first tick
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