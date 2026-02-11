using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winsane.UI.ViewModels;

public partial class ItemGroupViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ItemViewModel> _items = new();

    [ObservableProperty]
    private string? _icon;

    public ItemGroupViewModel(string header, string? icon = null)
    {
        Header = header;
        if (!string.IsNullOrEmpty(icon))
        {
            Icon = icon;
        }
    }
}
