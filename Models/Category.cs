using YamlDotNet.Serialization;

namespace WinsaneCS.Models;

/// <summary>
/// A category within a feature (e.g., "General", "Visuals", "Browsers")
/// </summary>
public class Category
{
    [YamlMember(Alias = "category")]
    public string Name { get; set; } = string.Empty;
    
    public List<Item> Items { get; set; } = new();
}
