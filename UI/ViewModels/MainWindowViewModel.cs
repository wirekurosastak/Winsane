using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly CoreService _coreService;

    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = new();

    [ObservableProperty]
    private FeatureViewModel? _selectedFeature;

    [ObservableProperty]
    private bool _isSettingsVisible;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

    public MainWindowViewModel(ConfigService configService, CoreService coreService)
    {
        _configService = configService;
        _coreService = coreService;

        SettingsViewModel = new SettingsViewModel();

        LoadConfigAsync();
    }

    private void LoadConfigAsync()
    {
        _ = LoadConfigCoreAsync();
    }

    private async Task LoadConfigCoreAsync()
    {
        await CreateBackupOnFirstRun();

        var config = await _configService.LoadConfigAsync();

        foreach (var feature in config.Features)
        {
            var featureVm = new FeatureViewModel(feature, _coreService, _configService, config);
            Features.Add(featureVm);
        }

        SettingsViewModel.Initialize(_configService, config, _coreService);

        if (Features.Count > 0)
        {
            SelectedFeature = Features[0];
        }
    }

    private async Task CreateBackupOnFirstRun()
    {
        string flagPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Winsane",
            ".setup_complete"
        );
        if (!File.Exists(flagPath))
        {
            try
            {
                await _coreService.CreateSystemRestorePointAsync("Winsane First Run Backup");

                File.WriteAllText(flagPath, DateTime.Now.ToString());
            }
            catch { }
        }
    }
}
