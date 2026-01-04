namespace Winsane.Core.Models;

/// <summary>
/// Root configuration loaded from data.yaml
/// </summary>
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
