using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class FeatureViewModel : ViewModelBase
{
    private readonly CoreService _coreService;
    private readonly ConfigService _configService;
    
    [ObservableProperty]
    private string _name;
    
    [ObservableProperty]
    private string _icon;
    
    [ObservableProperty]
    private ObservableCollection<object> _leftColumnItems = new();
    
    [ObservableProperty]
    private ObservableCollection<object> _rightColumnItems = new();
    
    [ObservableProperty]
    private bool _isDashboard;
    
    [ObservableProperty]
    private DashboardViewModel? _dashboard;
    
    public FeatureViewModel(
        Feature feature, 
        CoreService coreService, 
        ConfigService configService, 
        AppConfig? config = null)
    {
        _coreService = coreService;
        _configService = configService;
        
        _name = feature.Name;
        _icon = feature.Icon ?? "Settings";
        _isDashboard = feature.Type?.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ?? false;
        
        if (_isDashboard)
        {
            Dashboard = new DashboardViewModel();
        }
        else
        {
            InitializeItems(feature, config);
        }
    }
    
    private void InitializeItems(Feature feature, AppConfig? config)
    {
        var rawItems = new List<object>();

        // 1. Process standard items
        if (feature.Items != null)
        {
            // Grouping logic: Iterate and look for Header (Category) items
            // Assuming the YAML structure is flat: Category -> Item -> Item -> Category -> Item
            
            int i = 0;
            while (i < feature.Items.Count)
            {
                var item = feature.Items[i];
                var itemVm = new ItemViewModel(item, _coreService, _configService);
                
                // Initialize checks asynchronously
                _ = itemVm.InitializeAsync();

                if (item.IsCategory)
                {
                    // Create a Group
                    var group = new ItemGroupViewModel(item.Category);
                    
                    // Look ahead for items belonging to this category
                    i++; 
                    while(i < feature.Items.Count && !feature.Items[i].IsCategory)
                    {
                        var subItem = feature.Items[i];
                        var subItemVm = new ItemViewModel(subItem, _coreService, _configService);
                        _ = subItemVm.InitializeAsync();
                        
                        group.Items.Add(subItemVm);
                        i++;
                    }
                    
                    rawItems.Add(group);
                    // Loop continues, 'i' is now at the next Category or End
                }
                else
                {
                    // Orphaned item (no category header above it), just add it directly
                    rawItems.Add(itemVm);
                    i++;
                }
            }
        }
        
        // 2. Add "Add Tweak" control if this feature supports it
        if (!string.IsNullOrEmpty(feature.UserTweaksSection) && config != null)
        {
             // We need to gather existing user tweaks to pass to the VM
             var existingUserTweaks = feature.Items?
                 .Where(x => x.IsUserTweak)
                 .Select(x => new ItemViewModel(x, _coreService, _configService))
                 .ToList() ?? new List<ItemViewModel>();

             var addTweakVm = new AddTweakViewModel(config, _configService, _coreService, existingUserTweaks);
             rawItems.Add(addTweakVm);
        }

        DistributeItems(rawItems);
    }
    
    private void DistributeItems(List<object> items)
    {
        // Even split for 2-column layout
        int splitIndex = (int)Math.Ceiling(items.Count / 2.0);
        
        var left = items.Take(splitIndex);
        var right = items.Skip(splitIndex);
        
        LeftColumnItems = new ObservableCollection<object>(left);
        RightColumnItems = new ObservableCollection<object>(right);
    }

    public void RefreshDashboard(SystemInfoService systemInfoService)
    {
        Dashboard?.Refresh(systemInfoService);
    }
}