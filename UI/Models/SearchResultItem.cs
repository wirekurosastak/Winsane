namespace Winsane.UI.Models;

public class SearchResultItem
{
    public required string Title { get; set; }
    public required string Subtitle { get; set; }
    public required object NavigationTarget { get; set; }
}
