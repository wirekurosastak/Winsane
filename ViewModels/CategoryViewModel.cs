using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinsaneCS.Models;
using WinsaneCS.Services;

namespace WinsaneCS.ViewModels;

public partial class CategoryViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly AppConfig? _config;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ItemViewModel> _items = new();
    
    [ObservableProperty]
    private ObservableCollection<object> _visualItems = new();
    
    [ObservableProperty]
    private ObservableCollection<object> _leftColumnItems = new();
    
    [ObservableProperty]
    private ObservableCollection<object> _rightColumnItems = new();
    
    [ObservableProperty]
    private bool _isUserCategory;
    
    [ObservableProperty]
    private AddTweakViewModel? _addTweakViewModel;
    
    public CategoryViewModel(
        Category category, 
        string featureName, 
        PowerShellService powerShellService, 
        WingetService wingetService, 
        ConfigService configService,
        AppConfig? config = null)
    {
        _configService = configService;
        _config = config;
        Name = category.Name;
        
        // Determine if this is Admin Tools (uses buttons instead of toggles)
        bool isAdminTools = featureName == "Admin Tools";
        bool isApps = featureName == "Apps";
        
        // Check if this is the User category under Optimizer
        IsUserCategory = featureName == "Optimizer" && category.Name == "User";
        
        // Create AddTweakViewModel for User category
        if (IsUserCategory && config != null)
        {
            AddTweakViewModel = new AddTweakViewModel(config, configService, powerShellService, wingetService, Items);
        }
        
        // Create item ViewModels
        var flatItems = new List<ItemViewModel>();
        foreach (var item in category.Items)
        {
            var itemVm = new ItemViewModel(item, powerShellService, wingetService, configService)
            {
                IsAppsFeature = isApps,
                IsUserTweak = IsUserCategory
            };
            
            if (IsUserCategory)
            {
                itemVm.OnDeleted += OnItemDeleted;
            }
            
            flatItems.Add(itemVm);
            Items.Add(itemVm); // Keep flat list populated
        }
        
        // Grouping Logic to create VisualItems
        int i = 0;
        while (i < flatItems.Count)
        {
            var item = flatItems[i];
            
            if (item.IsHeader)
            {
                // Look ahead
                var futureItems = flatItems.Skip(i + 1).TakeWhile(x => !x.IsHeader && !x.IsUserTweak).ToList();
                
                // If we have items and NONE of them have subitems (Complex)
                if (futureItems.Any() && futureItems.All(x => !x.HasSubItems))
                {
                    var group = new ItemGroupViewModel(item.Header ?? string.Empty);
                    foreach(var f in futureItems) group.Items.Add(f);
                    VisualItems.Add(group);
                    i += 1 + futureItems.Count; // Skip header + items
                    continue;
                }
            }
            
            // Fallback: Add standalone
            VisualItems.Add(item);
            i++;
        }
        
        // Split Logic: Fill Left Column first, then Right Column (Vertical Split)
        int splitIndex = (int)Math.Ceiling(VisualItems.Count / 2.0);
        
        // Adjust split index to avoid separating a Header from its Item
        // If the item AT the split line is NOT a header, but the one BEFORE it IS a header,
        // we should move the split point back so the header joins the item in the second column.
        if (splitIndex > 0 && splitIndex < VisualItems.Count)
        {
            var itemBeforeSplit = VisualItems[splitIndex - 1] as ItemViewModel;
            if (itemBeforeSplit != null && itemBeforeSplit.IsHeader)
            {
                splitIndex--;
            }
        }
        
        for (int j = 0; j < VisualItems.Count; j++)
        {
            if (j < splitIndex)
                LeftColumnItems.Add(VisualItems[j]);
            else
                RightColumnItems.Add(VisualItems[j]);
        }
    }
    
    private async void OnItemDeleted(ItemViewModel item)
    {
        if (_config == null || string.IsNullOrEmpty(item.Name)) return;
        
        // Remove from config
        var success = await _configService.DeleteUserTweakAsync(_config, item.Name);
        
        if (success)
        {
            // Unsubscribe and remove from UI
            item.OnDeleted -= OnItemDeleted;
            Items.Remove(item);
        }
    }
}
