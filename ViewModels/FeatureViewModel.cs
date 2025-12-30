using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinsaneCS.Models;
using WinsaneCS.Services;

namespace WinsaneCS.ViewModels;

public partial class FeatureViewModel : ViewModelBase
{
    private readonly PowerShellService _powerShellService;
    private readonly WingetService _wingetService;
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
    
    private readonly ObservableCollection<ItemViewModel> _allItems = new();
    
    public FeatureViewModel(Feature feature, PowerShellService powerShellService, WingetService wingetService, ConfigService configService, AppConfig? config = null)
    {
        _powerShellService = powerShellService;
        _wingetService = wingetService;
        _configService = configService;
        _config = config;
        
        Name = feature.Name;
        Icon = GetIconForFeature(feature.Name);
        IsDashboard = feature.Name == "Dashboard";
        
        if (IsDashboard)
        {
            Dashboard = new DashboardViewModel();
        }
        else
        {
            InitializeItems(feature, powerShellService, wingetService, configService, config);
        }
    }
    
    private void InitializeItems(Feature feature, PowerShellService powerShellService, WingetService wingetService, ConfigService configService, AppConfig? config)
    {
        bool isApps = feature.Name == "Apps";
        bool isOptimizer = feature.Name == "Optimizer";
        
        // 1. Convert models to ViewModels
        var visualItems = new List<object>();
        var flatItemVms = new List<ItemViewModel>();
        var userTweaks = new List<ItemViewModel>();

        if (feature.Items != null)
        {
            foreach (var item in feature.Items)
            {
                var itemVm = new ItemViewModel(item, powerShellService, wingetService, configService)
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
                    _allItems.Add(itemVm);
                }
            }
        }

        // 2. Group items (Headers logic)
        int i = 0;
        while (i < flatItemVms.Count)
        {
            var item = flatItemVms[i];
            
            if (item.IsHeader)
            {
                // Look ahead for items belonging to this header
                // We stop at the next header OR at a user tweak (unless we want user tweaks inside groups?)
                // Actually, user tweaks are just items.
                var futureItems = flatItemVms.Skip(i + 1).TakeWhile(x => !x.IsHeader).ToList();
                
                // If we have items and NONE of them have subitems (Complex subitems usually get their own row)
                if (futureItems.Any() && futureItems.All(x => !x.HasSubItems))
                {
                    var group = new ItemGroupViewModel(item.Header ?? string.Empty);
                    foreach(var f in futureItems) group.Items.Add(f);
                    visualItems.Add(group);
                    i += 1 + futureItems.Count; // Skip header + items
                    continue;
                }
            }
            
            // Fallback: Add standalone
            visualItems.Add(item);
            i++;
        }
        
        // 3. Add "Add Tweak" item if Optimizer
        if (isOptimizer && config != null)
        {
             // Pass existing user tweaks
             var addTweakVm = new AddTweakViewModel(config, configService, powerShellService, wingetService, userTweaks);
             visualItems.Add(addTweakVm);
        }

        // 4. Split into Columns
        DistributeItems(visualItems);
    }
    

    
    private void DistributeItems(List<object> items)
    {
        int splitIndex = (int)Math.Ceiling(items.Count / 2.0);
        
        for (int j = 0; j < items.Count; j++)
        {
            if (j < splitIndex)
                LeftColumnItems.Add(items[j]);
            else
                RightColumnItems.Add(items[j]);
        }
    }
    


    [ObservableProperty]
    private bool _showCategories; // Unused now, but kept to avoid binding errors until View is updated
    
    public void RefreshDashboard(SystemInfoService systemInfoService)
    {
        Dashboard?.Refresh(systemInfoService);
    }
    
    private static string GetIconForFeature(string featureName)
    {
        return featureName switch
        {
            "Optimizer" => "Settings",
            "Cleaner" => "Delete",
            "Apps" => "AllApps",
            "Admin Tools" => "Admin",
            "Dashboard" => "Home",
            _ => "Settings"
        };
    }
}
