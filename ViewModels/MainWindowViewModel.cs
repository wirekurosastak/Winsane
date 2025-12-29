using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinsaneCS.Models;
using WinsaneCS.Services;

namespace WinsaneCS.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly PowerShellService _powerShellService;
    private readonly WingetService _wingetService;
    
    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = new();
    
    [ObservableProperty]
    private FeatureViewModel? _selectedFeature;
    
    [ObservableProperty]
    private bool _isSettingsVisible;
    
    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;
    
    public MainWindowViewModel()
    {
        _configService = new ConfigService();
        _powerShellService = new PowerShellService();
        _wingetService = new WingetService();
        _settingsViewModel = new SettingsViewModel();
        
        LoadConfigAsync();
    }
    
    private async void LoadConfigAsync()
    {
        try
        {
            var config = await _configService.LoadConfigAsync();
            
            foreach (var feature in config.Features)
            {
                var featureVm = new FeatureViewModel(feature, _powerShellService, _wingetService, _configService, config);
                Features.Add(featureVm);
            }
            
            // Select first feature by default
            if (Features.Count > 0)
            {
                SelectedFeature = Features[0];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }
    }
}
