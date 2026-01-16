using YamlDotNet.Serialization;

namespace Winsane.Core.Models;

/// <summary>
/// An individual tweak/app item with PowerShell commands.
/// Assumes data.yaml is valid and trusted.
/// </summary>
public class Item
{
    [YamlMember(Alias = "category")]
    public string Category { get; set; } = string.Empty;
    
    // Regular items
    public string Name { get; set; } = "Unknown Item";
    public string Purpose { get; set; } = string.Empty;
    
    [YamlMember(Alias = "true")]
    public string TrueCommand { get; set; } = string.Empty;
    
    [YamlMember(Alias = "false")]
    public string FalseCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "check")]
    public string CheckCommand { get; set; } = string.Empty;
    
    [YamlMember(Alias = "subitems")]
    public List<Item> SubItems { get; set; } = new();

    /// <summary>
    /// Returns true if this is a category/header separator.
    /// </summary>
    public bool IsCategory => !string.IsNullOrEmpty(Category);
    
    /// <summary>
    /// Returns true if this item is irreversible (no false command provided).
    /// </summary>
    public bool IsIrreversible => string.IsNullOrEmpty(FalseCommand);
    
    /// <summary>
    /// Flag for user-created tweaks (runtime only).
    /// </summary>
    [YamlMember(Alias = "is_user_tweak")]
    public bool IsUserTweak { get; set; } = false;
}