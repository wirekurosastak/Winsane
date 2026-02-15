using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Styling;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _backupStatusText = string.Empty;
    [ObservableProperty] private bool _isBackingUp;
    private CoreService? _coreService;

    public void Initialize(CoreService? coreService = null) => _coreService = coreService;

    [RelayCommand]
    private async Task CreateBackup()
    {
        if (_coreService == null || IsBackingUp) return;

        IsBackingUp = true;
        BackupStatusText = "Creating System Restore Point...";
        try
        {
            bool success = await _coreService.CreateSystemRestorePointAsync("Winsane Manual Backup");
            BackupStatusText = success ? "Backup Successful!" : "Backup Failed (Requires Admin?)";
        }
        catch (Exception ex) { BackupStatusText = $"Error: {ex.Message}"; }
        finally { IsBackingUp = false; }
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }
}
