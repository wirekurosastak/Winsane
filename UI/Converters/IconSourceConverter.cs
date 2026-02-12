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
            // 1. Try to parse as Symbol enum (e.g. "Home", "Settings")
            if (Enum.TryParse<Symbol>(iconName, out var symbol))
            {
                return new SymbolIconSource { Symbol = symbol };
            }

            // 2. Try to parse as hex glyph code
            // Supports formats: "&#xE708;", "xE708", "E708", "\uE708"
            string? hexCode = null;
            if (iconName.StartsWith("&#x", StringComparison.OrdinalIgnoreCase) && iconName.EndsWith(";"))
            {
                hexCode = iconName.Substring(3, iconName.Length - 4);
            }
            else if (iconName.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
            {
                hexCode = iconName.Substring(2);
            }
            else if (iconName.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                hexCode = iconName.Substring(1);
            }
            else if (iconName.Length == 4 && int.TryParse(iconName, System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                 hexCode = iconName;
            }

            if (hexCode != null && int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out int glyphCode))
            {
                return new FontIconSource
                {
                    Glyph = char.ConvertFromUtf32(glyphCode),
                    FontFamily = new FontFamily("Segoe Fluent Icons")
                };
            }

            // 3. Fallback (removed as per request)

        }
        return new SymbolIconSource { Symbol = Symbol.Help };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}