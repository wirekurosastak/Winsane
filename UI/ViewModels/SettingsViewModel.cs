using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Styling;
using Winsane.Core.Interfaces;
using Winsane.Core.Models;

namespace Winsane.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedThemeIndex = 0;  // 0=System, 1=Light, 2=Dark
    
    [ObservableProperty]
    private Color _accentColor = Color.Parse("#0078D4");
    
    [ObservableProperty]
    private string _accentColorHex = "#0078D4";
    
    [ObservableProperty]
    private string _backupStatusText = string.Empty;
    
    [ObservableProperty]
    private bool _isBackingUp;
    
    // Preset accent colors
    public Color[] PresetColors { get; } = new[]
    {
        Color.Parse("#E74856"),  // Red
        Color.Parse("#FF8C00"),  // Orange
        Color.Parse("#FFB900"),  // Yellow
        Color.Parse("#107C10"),  // Green
        Color.Parse("#00B294"),  // Mint
        Color.Parse("#018574"),  // Dark Teal
        Color.Parse("#0099BC"),  // Teal
        Color.Parse("#0078D4"),  // Blue
        Color.Parse("#0063B1"),  // Dark Blue
        Color.Parse("#744DA9"),  // Violet
        Color.Parse("#8764B8"),  // Purple
        Color.Parse("#E91E63"),  // Pink
        Color.Parse("#C30052"),  // Rose
        Color.Parse("#767676"),  // Gray
    };
    
    private IConfigService? _configService;
    private ICoreService? _coreService;
    private AppConfig? _config;

    public void Initialize(IConfigService configService, AppConfig config, ICoreService? coreService = null)
    {
        _configService = configService;
        _config = config;
        _coreService = coreService;
        
        // Restore saved theme setting if present
        if (_config?.Theme != null)
        {
             if (!string.IsNullOrEmpty(_config.Theme.AccentColor))
             {
                 try 
                 {
                     var savedColor = Color.Parse(_config.Theme.AccentColor);
                     AccentColor = savedColor;
                 }
                 catch {}
             }
        }
    }

    public SettingsViewModel()
    {
        // Load current theme
        var faTheme = Application.Current?.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
        if (faTheme != null)
        {
            // Get current theme from app
            var currentTheme = Application.Current?.RequestedThemeVariant;
            if (currentTheme == ThemeVariant.Light)
                SelectedThemeIndex = 1;
            else if (currentTheme == ThemeVariant.Dark)
                SelectedThemeIndex = 2;
            else
                SelectedThemeIndex = 0;
            
            // Get accent color
            if (faTheme.CustomAccentColor.HasValue)
            {
                AccentColor = faTheme.CustomAccentColor.Value;
            }
            else
            {
                 // Try to get system accent color
                 if (Application.Current != null && Application.Current.TryGetResource("SystemAccentColor", null, out var res) && res is Color color)
                 {
                     AccentColor = color;
                 }
            }
        }
    }
    
    partial void OnSelectedThemeIndexChanged(int value)
    {
        var app = Application.Current;
        if (app == null) return;
        
        app.RequestedThemeVariant = value switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default  // System
        };
    }

    partial void OnAccentColorChanged(Color value)
    {
        var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        if (!string.Equals(AccentColorHex, hex, StringComparison.OrdinalIgnoreCase))
        {
            AccentColorHex = hex;
        }
        
        ApplyAccentColor(value);
        
        // Save to config
        if (_config != null && _configService != null)
        {
            if (_config.Theme == null) _config.Theme = new Winsane.Core.Models.ThemeConfig();
            _config.Theme.AccentColor = hex;
            _ = _configService.SaveConfigAsync(_config);
        }
    }
    
    partial void OnAccentColorHexChanged(string value)
    {
        if (Color.TryParse(value, out var color))
        {
            if (AccentColor != color)
            {
                AccentColor = color;
            }
        }
    }
    
    [RelayCommand]
    private void SetAccentColor(Color color)
    {
        AccentColor = color;
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
