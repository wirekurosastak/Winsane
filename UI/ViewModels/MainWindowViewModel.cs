using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Interfaces;
using Winsane.Core.Models;

namespace Winsane.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly ICoreService _coreService;
    
    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = new();
    
    [ObservableProperty]
    private FeatureViewModel? _selectedFeature;
    
    [ObservableProperty]
    private bool _isSettingsVisible;
    
    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;
    
    public MainWindowViewModel(
        IConfigService configService, 
        ICoreService coreService)
    {
        _configService = configService;
        _coreService = coreService;
        
        SettingsViewModel = new SettingsViewModel();
        
        LoadConfigAsync();
    }

    /// <summary>
    /// Design-time constructor
    /// </summary>

    
    private void LoadConfigAsync()
    {
        // Fire and forget with proper exception handling
        _ = LoadConfigCoreAsync();
    }
    
    private async Task LoadConfigCoreAsync()
    {
        try
        {
            await CreateBackupOnFirstRun();

            var config = await _configService.LoadConfigAsync();
            
            foreach (var feature in config.Features)
            {
                var featureVm = new FeatureViewModel(feature, _coreService, _configService, config);
                Features.Add(featureVm);
            }
            
            // Initialize Settings with config
            SettingsViewModel.Initialize(_configService, config, _coreService);
            
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

    private async Task CreateBackupOnFirstRun()
    {
        string flagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane", ".setup_complete");
        if (!File.Exists(flagPath))
        {
             // It's the first run!
             try
             {
                 // Create Restore Point
                 await _coreService.CreateSystemRestorePointAsync("Winsane First Run Backup");
                 
                 // Mark as complete
                 File.WriteAllText(flagPath, DateTime.Now.ToString());
             }
             catch
             {
                 // Backup failed, maybe not admin?
                 // Should we warn user? For now just silent fail as it's background.
             }
        }
    }
}
