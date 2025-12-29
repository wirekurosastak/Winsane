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
    
    [ObservableProperty]
    private ObservableCollection<CategoryViewModel> _categories = new();
    
    [ObservableProperty]
    private CategoryViewModel? _selectedCategory;
    
    // Dashboard-specific properties
    [ObservableProperty]
    private bool _isDashboard;
    
    [ObservableProperty]
    private DashboardViewModel? _dashboard;
    
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
            // Create category ViewModels
            foreach (var category in feature.Categories)
            {
                var categoryVm = new CategoryViewModel(category, feature.Name, powerShellService, wingetService, configService, config);
                Categories.Add(categoryVm);
            }
            
            ShowCategories = Categories.Count > 1;
            
            // Select first category by default
            if (Categories.Any())
            {
                SelectedCategory = Categories.First();
            }
        }
    }

    [ObservableProperty]
    private bool _showCategories;
    
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
            "Display" => "TVMonitor",
            _ => "Settings"
        };
    }
}
