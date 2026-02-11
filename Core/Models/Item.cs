using YamlDotNet.Serialization;

namespace Winsane.Core.Models;

public class Item
{
    [YamlMember(Alias = "category")]
    public string Category { get; set; } = string.Empty;

    public string Name { get; set; } = "Unknown Item";
    public string Purpose { get; set; } = string.Empty;

    [YamlMember(Alias = "icon")]
    public string? Icon { get; set; }

    [YamlMember(Alias = "true")]
    public string TrueCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "false")]
    public string FalseCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "button")]
    public string ButtonCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "button_text")]
    public string ButtonText { get; set; } = string.Empty;

    [YamlMember(Alias = "check")]
    public string CheckCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "subitems")]
    public List<Item> SubItems { get; set; } = new();

    public bool IsCategory => !string.IsNullOrEmpty(Category);

    [YamlMember(Alias = "is_user_tweak")]
    public bool IsUserTweak { get; set; } = false;
}
