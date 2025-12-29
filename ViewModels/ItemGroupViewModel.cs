using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinsaneCS.ViewModels;

public partial class ItemGroupViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _header = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ItemViewModel> _items = new();
    
    public ItemGroupViewModel(string header)
    {
        Header = header;
    }
}
