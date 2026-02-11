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
            // 1. Próbáljuk meg a beépített Symbol listából (pl. "Settings", "Home")
            if (Enum.TryParse<Symbol>(iconName, out var symbol))
            {
                return new SymbolIconSource { Symbol = symbol };
            }

            string glyph = iconName switch
            {
                "PowerButton" => "\uE7E8",   // ⏨ Kikapcsoló gomb
                "Accessibility" => "\uE776", // ♿ Kerekesszékes / Kisegítő ikon
                "User" => "\uE77B",          // 👤 Felhasználó
                "Gaming" => "\uE7FC",        // 🎮 Játék kontroller
                "Shield" => "\uEA18",        // 🛡️ Pajzs (Security)
                "Broom" => "\uE894",         // 🧹 Seprű (Cleaner alternatíva)
                "Terminal" => "\uE756",      // 📟 Konzol/Terminál
                "Performance" => "\uEC4A",   // 📊 Teljesítmény
                _ => ""
            };

            if (!string.IsNullOrEmpty(glyph))
            {
                return new FontIconSource
                {
                    Glyph = glyph,
                    // A FluentAvalonia alapértelmezetten tartalmazza a megfelelő fontot,
                    // de ha biztosra akarsz menni, megadhatod:
                    FontFamily = new FontFamily("Segoe Fluent Icons") 
                };
            }
        }
        
        // Ha semmi nem talált, egy kérdőjelet adunk vissza
        return new SymbolIconSource { Symbol = Symbol.Help };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}