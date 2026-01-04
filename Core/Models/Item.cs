using YamlDotNet.Serialization;

namespace Winsane.Core.Models;

/// <summary>
/// An individual tweak/app item with PowerShell commands
/// </summary>
public class Item
{
    [YamlMember(Alias = "category")]
    public string? Category { get; set; }
    
    // Regular items
    public string? Name { get; set; }
    public string? Purpose { get; set; }
    
    [YamlMember(Alias = "true")]
    public string? TrueCommand { get; set; }
    
    [YamlMember(Alias = "false")]
    public string? FalseCommand { get; set; }

    [YamlMember(Alias = "check")]
    public string? CheckCommand { get; set; }

    [YamlMember(Alias = "id")]
    public string? PackageId { get; set; }
    
    [YamlMember(Alias = "subitems")]
    public List<Item> SubItems { get; set; } = new();

    
    /// <summary>
    /// Returns true if this is a category/header separator, not an actual item
    /// </summary>
    public bool IsCategory => !string.IsNullOrEmpty(Category);
    
    /// <summary>
    /// Returns true if this item is irreversible (no false command)
    /// </summary>
    public bool IsIrreversible => string.IsNullOrEmpty(FalseCommand);
    
    /// <summary>
    /// Flag for user-created tweaks (not serialized, set at runtime)
    /// </summary>
    [YamlMember(Alias = "is_user_tweak")]
    public bool IsUserTweak { get; set; } = false;
}
