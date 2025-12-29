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
        foreach (var item in category.Items)
        {
            var itemVm = new ItemViewModel(item, powerShellService, wingetService, configService)
            {
                IsAppsFeature = isApps,
                IsUserTweak = IsUserCategory  // User category items are deletable
            };
            
            // Subscribe to delete event for user tweaks
            if (IsUserCategory)
            {
                itemVm.OnDeleted += OnItemDeleted;
            }
            
            Items.Add(itemVm);
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
