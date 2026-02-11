using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winsane.UI.ViewModels;

public partial class PowerViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _hours;

    [ObservableProperty]
    private int _minutes;

    [ObservableProperty]
    private int _seconds;

    [RelayCommand]
    private async Task Shutdown()
    {
        await RunPowerCommand("-s");
    }

    [RelayCommand]
    private async Task Restart()
    {
        await RunPowerCommand("-r");
    }

    [RelayCommand]
    private async Task Bios()
    {
        await RunPowerCommand("-r -fw");
    }

    [RelayCommand]
    private void Cancel()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "-a",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch
        {
            // Ignore if no shutdown was in progress
        }
    }

    private async Task RunPowerCommand(string actionArgs)
    {
        int totalSeconds = (Hours * 3600) + (Minutes * 60) + Seconds;
        
        // Ensure at least 0
        if (totalSeconds < 0) totalSeconds = 0;

        string args = $"{actionArgs} -f -t {totalSeconds}";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            
            Process.Start(psi);
        }
        catch
        {
            // TODO: Handle error or show notification
        }
    }
}
