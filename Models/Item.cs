using YamlDotNet.Serialization;

namespace WinsaneCS.Models;

/// <summary>
/// An individual tweak/app item with PowerShell commands
/// </summary>
public class Item
{
    // Header items only have this property
    public string? Header { get; set; }
    
    // Regular items
    public string? Name { get; set; }
    public string? Purpose { get; set; }
    
    [YamlMember(Alias = "true")]
    public string? TrueCommand { get; set; }
    
    [YamlMember(Alias = "false")]
    public string? FalseCommand { get; set; }

    [YamlMember(Alias = "id")]
    public string? PackageId { get; set; }
    
    [YamlMember(Alias = "subitems")]
    public List<Item> SubItems { get; set; } = new();

    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Returns true if this is a header separator, not an actual item
    /// </summary>
    public bool IsHeader => !string.IsNullOrEmpty(Header);
    
    /// <summary>
    /// Returns true if this item is irreversible (no false command)
    /// </summary>
    public bool IsIrreversible => string.IsNullOrEmpty(FalseCommand);
    
    /// <summary>
    /// Flag for user-created tweaks (not serialized, set at runtime)
    /// </summary>
    [YamlIgnore]
    public bool IsUserTweak { get; set; } = false;
}
