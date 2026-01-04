using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Styling;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;
using Microsoft.Win32;

namespace Winsane.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private Color _accentColor = Color.Parse("#0078D4");
    
    [ObservableProperty]
    private string _backupStatusText = string.Empty;
    
    [ObservableProperty]
    private bool _isBackingUp;
    
    private ConfigService? _configService;
    private CoreService? _coreService;
    private AppConfig? _config;

    public void Initialize(
        ConfigService configService, 
        AppConfig config, 
        CoreService? coreService = null)
    {
        _configService = configService;
        _config = config;
        _coreService = coreService;
        
        // Always detect system defaults on startup
        DetectSystemDefaults();
    }

    private void DetectSystemDefaults()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            // Accent Color
            using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (dwmKey != null)
            {
                var colorVal = dwmKey.GetValue("AccentColor") as int?;
                if (colorVal.HasValue)
                {
                    // Windows stores color as 0x00BBGGRR
                    var bytes = BitConverter.GetBytes(colorVal.Value);
                    AccentColor = Color.FromRgb(bytes[0], bytes[1], bytes[2]);
                }
            }
        }
        catch { }
    }

    public SettingsViewModel()
    {
        // Design-time or pre-init
    }
    
    partial void OnAccentColorChanged(Color value)
    {
        ApplyAccentColor(value);
    }
    
    private void ApplyAccentColor(Color color)
    {
        var faTheme = Application.Current?.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
        if (faTheme != null)
        {
            faTheme.CustomAccentColor = color;
        }
    }

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
        catch (Exception ex)
        {
            BackupStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBackingUp = false;
        }
    }
    
    [RelayCommand]
    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* Ignore browser errors */ }
    }
}
