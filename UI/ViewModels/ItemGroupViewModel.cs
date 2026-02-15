using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winsane.UI.ViewModels;

public partial class ItemGroupViewModel : ViewModelBase
{
    [ObservableProperty] private string _header = string.Empty;
    [ObservableProperty] private ObservableCollection<ItemViewModel> _items = new();
    [ObservableProperty] private string? _icon;
    [ObservableProperty] private int? _column;

    public ItemGroupViewModel(string header, string? icon = null, int? column = null)
    {
        Header = header;
        Column = column;
        if (!string.IsNullOrEmpty(icon)) Icon = icon;
    }
}
