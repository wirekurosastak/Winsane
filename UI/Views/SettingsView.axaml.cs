using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Layout;
using Winsane.UI.ViewModels;
using System;
using System.Globalization;

namespace Winsane.UI.Views;

public partial class SettingsView : UserControl
{
    public static readonly ColorToBrushConverter ColorToBrushConverter = new();
    
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }
    
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            PopulateColorPalette(vm);
        }
    }
    
    private void PopulateColorPalette(SettingsViewModel vm)
    {
        ColorPalette.Children.Clear();
        
        foreach (var color in vm.PresetColors)
        {
            var button = new Button
            {
                Width = 40,
                Height = 40,
                Margin = new Avalonia.Thickness(4),
                Command = vm.SetAccentColorCommand,
                CommandParameter = color
            };
            
            // Create a custom template with the color
            button.Content = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new Avalonia.CornerRadius(4),
                Width = 36,
                Height = 36};
            
            ColorPalette.Children.Add(button);
        }
    }
}

public class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return new SolidColorBrush(color);
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
