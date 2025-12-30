using YamlDotNet.Serialization;

namespace WinsaneCS.Models;

/// <summary>
/// A top-level feature (Optimizer, Cleaner, Apps, Admin Tools, Dashboard)
/// </summary>
public class Feature
{
    [YamlMember(Alias = "feature")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional: Items directly on the feature
    /// </summary>
    public List<Item> Items { get; set; } = new();
    
    // Dashboard-specific layout (optional)
    public DashboardLayout? Layout { get; set; }
}

public class DashboardLayout
{
    public List<DashboardItem> Left { get; set; } = new();
    public List<DashboardItem> Right { get; set; } = new();
}

public class DashboardItem
{
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Default { get; set; }
    
    [YamlMember(Alias = "source_key")]
    public string? SourceKey { get; set; }
    
    public int? Wrap { get; set; }
}
