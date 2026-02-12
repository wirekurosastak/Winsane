using YamlDotNet.Serialization;

namespace Winsane.Core.Models;

public class Feature
{
    [YamlMember(Alias = "feature")]
    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }
    public string? Type { get; set; }

    [YamlMember(Alias = "user_tweaks_section")]
    public string? UserTweaksSection { get; set; }

    public List<Item> Items { get; set; } = new();

    [YamlIgnore]
    public List<Item> UserTweaks { get; set; } = new();
}
