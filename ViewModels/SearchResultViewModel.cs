namespace WinsaneCS.ViewModels;

/// <summary>
/// Search result item for the search box
/// </summary>
public class SearchResultViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;  // e.g., "Optimizer > Performance"
    public string FeatureName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public ItemViewModel? Item { get; set; }
    
    public override string ToString() => Name;
}
