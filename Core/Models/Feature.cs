using YamlDotNet.Serialization;

namespace Winsane.Core.Models;

/// <summary>
/// A top-level feature (Optimizer, Cleaner, Apps, Admin Tools, Dashboard)
/// </summary>
public class Feature
{
    [YamlMember(Alias = "feature")]
    public string Name { get; set; } = string.Empty;
    
    // Data-driven UI/Behavior properties
    public string? Icon { get; set; }
    public string? Type { get; set; } // "standard", "apps", "dashboard"
    
    [YamlMember(Alias = "user_tweaks_section")]
    public string? UserTweaksSection { get; set; }
    
    /// <summary>
    /// Optional: Items directly on the feature
    /// </summary>
    public List<Item> Items { get; set; } = new();
}
