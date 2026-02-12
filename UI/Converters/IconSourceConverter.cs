using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;
using Avalonia.Media;

namespace Winsane.UI.Converters;

public class IconSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string iconName)
        {
            if (Enum.TryParse<Symbol>(iconName, out var symbol))
            {
                return new SymbolIconSource { Symbol = symbol };
            }

            string glyph = iconName switch
            {
                "PowerButton" => "\uE7E8",
                "Accessibility" => "\uE776",
                "User" => "\uE77B",
                "Gaming" => "\uE7FC",
                "Shield" => "\uEA18",
                "Broom" => "\uE894",
                "Terminal" => "\uE756",
                "Performance" => "\uEC4A",
                _ => ""
            };

            if (!string.IsNullOrEmpty(glyph))
            {
                return new FontIconSource
                {
                    Glyph = glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons") 
                };
            }
        }
        return new SymbolIconSource { Symbol = Symbol.Help };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}