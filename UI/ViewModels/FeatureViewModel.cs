using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class FeatureViewModel : ViewModelBase
{
    private readonly CoreService _coreService;
    private readonly ConfigService _configService;
    private readonly AppConfig? _config;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _icon = "Settings";
    
    // Flattened items logic
    [ObservableProperty]
    private ObservableCollection<object> _leftColumnItems = new();
    
    [ObservableProperty]
    private ObservableCollection<object> _rightColumnItems = new();
    
    // Dashboard-specific properties
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
        _config = config;
        
        Name = feature.Name;
        Icon = feature.Icon ?? "Settings";
        IsDashboard = feature.Type?.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ?? false;
        
        if (IsDashboard)
        {
            Dashboard = new DashboardViewModel();
        }
        else
        {
            InitializeItems(feature, coreService, configService, config);
        }
    }
    
    private void InitializeItems(
        Feature feature, 
        CoreService coreService, 
        ConfigService configService, 
        AppConfig? config)
    {
        bool isApps = feature.Type?.Equals("apps", StringComparison.OrdinalIgnoreCase) ?? false;
        
        // 1. Convert models to ViewModels
        var flatItemVms = new List<ItemViewModel>();
        var userTweaks = new List<ItemViewModel>();

        if (feature.Items != null)
        {
            foreach (var item in feature.Items)
            {
                var itemVm = new ItemViewModel(item, coreService, configService)
                {
                    IsAppsFeature = isApps,
                    IsUserTweak = item.IsUserTweak
                };
                
                if (item.IsUserTweak)
                {
                    userTweaks.Add(itemVm);
                }
                else
                {
                    flatItemVms.Add(itemVm);
                }
            }
        }

        // 2. Group items (Headers logic)
        var groupedItems = new List<object>();
        
        int i = 0;
        while (i < flatItemVms.Count)
        {
            var item = flatItemVms[i];
            
            if (item.IsHeader)
            {
                // Look ahead for items belonging to this header
                var futureItems = flatItemVms.Skip(i + 1).TakeWhile(x => !x.IsHeader).ToList();
                
                // If we have items and NONE of them have subitems (Complex subitems usually get their own row)
                if (futureItems.Any() && futureItems.All(x => !x.HasSubItems))
                {
                    var group = new ItemGroupViewModel(item.Header ?? string.Empty);
                    foreach(var f in futureItems) group.Items.Add(f);
                    groupedItems.Add(group);
                    i += 1 + futureItems.Count; // Skip header + items
                    continue;
                }
            }
            
            // Fallback: Add standalone
            groupedItems.Add(item);
            i++;
        }
        
        // 3. Add "Add Tweak" item if Allowed
        if (!string.IsNullOrEmpty(feature.UserTweaksSection) && config != null)
        {
             // Pass existing user tweaks
             var addTweakVm = new AddTweakViewModel(config, configService, _coreService, userTweaks);
             groupedItems.Add(addTweakVm);
        }

        // 4. Split into Columns
        DistributeItems(groupedItems);
    }
    
    private void DistributeItems(List<object> items)
    {
        // Simple even split
        int splitIndex = (int)Math.Ceiling(items.Count / 2.0);
        
        var left = new List<object>();
        var right = new List<object>();

        for (int j = 0; j < items.Count; j++)
        {
            if (j < splitIndex)
                left.Add(items[j]);
            else
                right.Add(items[j]);
        }
        
        LeftColumnItems = new ObservableCollection<object>(left);
        RightColumnItems = new ObservableCollection<object>(right);
    }

    public void RefreshDashboard(SystemInfoService systemInfoService)
    {
        Dashboard?.Refresh(systemInfoService);
    }
}
