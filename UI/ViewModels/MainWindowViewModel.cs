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

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = "Loading...";

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
        IsBusy = true;
        BusyMessage = "Initializing...";

        await CreateBackupOnFirstRun();

        BusyMessage = "Reading System Configuration...";
        var config = await _configService.LoadConfigAsync();

        var initializationTasks = new List<Task>();

        foreach (var feature in config.Features)
        {
            var featureVm = new FeatureViewModel(feature, _coreService, _configService, config);
            initializationTasks.Add(featureVm.InitializationTask);
            Features.Add(featureVm);
        }

        await Task.WhenAll(initializationTasks);

        SettingsViewModel.Initialize(_configService, config, _coreService);

        if (Features.Count > 0)
        {
            SelectedFeature = Features[0];
        }

        IsBusy = false;
    }

    private async Task CreateBackupOnFirstRun()
    {
        try
        {
            const string backupName = "Winsane First Run Backup";
            if (await _coreService.RestorePointExistsAsync(backupName))
            {
                return;
            }

            BusyMessage = "Creating First Run Backup...";
            await _coreService.CreateSystemRestorePointAsync(backupName);
        }
        catch { }
    }
}
