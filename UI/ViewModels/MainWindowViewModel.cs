using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

using Winsane.UI.Models;
using CommunityToolkit.Mvvm.Input;

namespace Winsane.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly CoreService _coreService;

    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = new();

    private List<SearchResultItem> _allSearchableItems = new();

    [ObservableProperty]
    private ObservableCollection<SearchResultItem> _searchResults = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SearchResultItem? _selectedSearchResult;

    [ObservableProperty]
    private bool _isSearchPopupOpen;

    partial void OnSelectedSearchResultChanged(SearchResultItem? value)
    {
        if (value != null)
        {
            NavigateToSearchResult(value);
            IsSearchPopupOpen = false;
        }
    }


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

        BusyMessage = "Reading Configuration...";
        var config = await _configService.LoadConfigAsync();

        var initializationTasks = new List<Task>();

        foreach (var feature in config.Features)
        {
            var featureVm = new FeatureViewModel(feature, _coreService, _configService, config);
            initializationTasks.Add(featureVm.InitializationTask);
            Features.Add(featureVm);
        }

        await Task.WhenAll(initializationTasks);

        SettingsViewModel.Initialize(_coreService);

        if (Features.Count > 0)
        {
            SelectedFeature = Features[0];
        }

        if (Features.Count > 0)
        {
            SelectedFeature = Features[0];
        }

        LoadSearchableItems();
        IsBusy = false;
    }

    private void LoadSearchableItems()
    {
        _allSearchableItems.Clear();

        foreach (var feature in Features)
        {
            // Add the feature itself
            _allSearchableItems.Add(new SearchResultItem
            {
                Title = feature.Name,
                Subtitle = "Feature",

                NavigationTarget = feature
            });

            if (feature.IsSystem) continue;

            // Helper to add items
            void AddItems(IEnumerable<object> items)
            {
                foreach (var item in items)
                {
                    if (item is ItemViewModel itemVm)
                    {
                        _allSearchableItems.Add(new SearchResultItem
                        {
                            Title = itemVm.Name,
                            Subtitle = feature.Name,

                            NavigationTarget = itemVm
                        });
                    }
                    else if (item is ItemGroupViewModel groupVm)
                    {
                        foreach (var subItem in groupVm.Items)
                        {
                            _allSearchableItems.Add(new SearchResultItem
                            {
                                Title = subItem.Name,
                                Subtitle = $"{feature.Name} > {groupVm.Header}",

                                NavigationTarget = subItem
                            });
                        }
                    }
                }
            }

            AddItems(feature.LeftColumnItems);
            AddItems(feature.MiddleColumnItems);
            AddItems(feature.RightColumnItems);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            IsSearchPopupOpen = false;
            return;
        }

        var results = _allSearchableItems
            .Where(x => x.Title.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                        x.Subtitle.Contains(value, StringComparison.OrdinalIgnoreCase))
            .Take(10) // Limit results
            .ToList();

        SearchResults = new ObservableCollection<SearchResultItem>(results);
        IsSearchPopupOpen = results.Any();
    }

    [RelayCommand]
    public void NavigateToSearchResult(SearchResultItem result)
    {
        if (result == null) return;

        if (result.NavigationTarget is FeatureViewModel feature)
        {
            SelectedFeature = feature;
        }
        else if (result.NavigationTarget is ItemViewModel item)
        {
           foreach(var f in Features)
           {
                if(GetAllItems(f).Contains(item))
                {
                    SelectedFeature = f;
                    break;
                }
           }
        }
        
        SearchText = "";
        SearchResults.Clear();
        IsSearchPopupOpen = false;
    }
    
    private IEnumerable<object> GetAllItems(FeatureViewModel feature)
    {
        var all = new List<object>();
        void Add(IEnumerable<object> items) 
        {
            foreach(var i in items)
            {
                if(i is ItemGroupViewModel g) all.AddRange(g.Items);
                else all.Add(i);
            }
        }
        Add(feature.LeftColumnItems);
        Add(feature.MiddleColumnItems);
        Add(feature.RightColumnItems);
        return all;
    }


    private async Task CreateBackupOnFirstRun()
    {
        try
        {
            string taskPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane", "Cleaner");
            Directory.CreateDirectory(taskPath);

            const string backupName = "Winsane First Run Backup";
            if (await _coreService.RestorePointExistsAsync(backupName))
            {
                return;
            }

            BusyMessage = "Creating Restore Point...";
            await _coreService.CreateSystemRestorePointAsync(backupName);
        }
        catch { }
    }
}
