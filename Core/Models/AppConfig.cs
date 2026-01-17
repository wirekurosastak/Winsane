namespace Winsane.Core.Models;

public class AppConfig
{
    public ThemeConfig? Theme { get; set; }
    public List<Feature> Features { get; set; } = new();
}

public class ThemeConfig
{
    public string? Mode { get; set; }
    public string? AccentColor { get; set; }
}
