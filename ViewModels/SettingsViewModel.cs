using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.Styling;

namespace WinsaneCS.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedThemeIndex = 0;  // 0=System, 1=Light, 2=Dark
    
    [ObservableProperty]
    private Color _accentColor = Color.Parse("#0078D4");
    
    [ObservableProperty]
    private string _accentColorHex = "#0078D4";
    
    // Preset accent colors
    public Color[] PresetColors { get; } = new[]
    {
        Color.Parse("#0078D4"),  // Windows Blue
        Color.Parse("#0099BC"),  // Teal
        Color.Parse("#00B294"),  // Green
        Color.Parse("#8764B8"),  // Purple
        Color.Parse("#E74856"),  // Red
        Color.Parse("#FF8C00"),  // Orange
        Color.Parse("#FFB900"),  // Yellow
        Color.Parse("#E91E63"),  // Pink
        Color.Parse("#744DA9"),  // Violet
        Color.Parse("#018574"),  // Dark Teal
        Color.Parse("#107C10"),  // Dark Green
        Color.Parse("#767676"),  // Gray
    };
    
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
                AccentColorHex = $"#{AccentColor.R:X2}{AccentColor.G:X2}{AccentColor.B:X2}";
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
        AccentColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        ApplyAccentColor(value);
    }
    
    [RelayCommand]
    private void SetAccentColor(Color color)
    {
        AccentColor = color;
    }
    
    [RelayCommand]
    private void ApplyHexColor()
    {
        try
        {
            var color = Color.Parse(AccentColorHex);
            AccentColor = color;
        }
        catch { /* Invalid hex */ }
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
