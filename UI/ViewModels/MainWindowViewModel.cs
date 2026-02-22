using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;
using Winsane.UI.Models;

namespace Winsane.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly CoreService _coreService;
    private List<SearchResultItem> _allSearchableItems = [];

    [ObservableProperty] private ObservableCollection<FeatureViewModel> _features = [];
    [ObservableProperty] private ObservableCollection<SearchResultItem> _searchResults = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private SearchResultItem? _selectedSearchResult;
    [ObservableProperty] private bool _isSearchPopupOpen;
    [ObservableProperty] private FeatureViewModel? _selectedFeature;
    [ObservableProperty] private bool _isSettingsVisible;
    [ObservableProperty] private SettingsViewModel _settingsViewModel;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = "Loading...";

    partial void OnSelectedSearchResultChanged(SearchResultItem? value)
    {
        if (value != null)
        {
            NavigateToSearchResult(value);
            IsSearchPopupOpen = false;
        }
    }

    public MainWindowViewModel(ConfigService configService, CoreService coreService)
    {
        _configService = configService;
        _coreService = coreService;
        SettingsViewModel = new SettingsViewModel();
        _ = LoadConfigCoreAsync();
    }

    private async Task LoadConfigCoreAsync()
    {
        IsBusy = true;
        BusyMessage = "Initializing...";
        await CreateBackupOnFirstRun();

        BusyMessage = "Reading Configuration...";
        var config = await _configService.LoadConfigAsync();
        var initTasks = new List<Task>();

        foreach (var feature in config.Features)
        {
            var featureVm = new FeatureViewModel(feature, _coreService, _configService, config);
            initTasks.Add(featureVm.InitializationTask);
            Features.Add(featureVm);
        }

        await Task.WhenAll(initTasks);
        SettingsViewModel.Initialize(_coreService);
        if (Features.Count > 0) SelectedFeature = Features[0];

        LoadSearchableItems();
        IsBusy = false;
    }

    private void LoadSearchableItems()
    {
        _allSearchableItems.Clear();

        void AddItems(FeatureViewModel feature, IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                if (item is ItemViewModel vm)
                    _allSearchableItems.Add(new SearchResultItem { Title = vm.Name, Subtitle = feature.Name, NavigationTarget = vm });
                else if (item is ItemGroupViewModel g)
                    foreach (var sub in g.Items)
                        _allSearchableItems.Add(new SearchResultItem { Title = sub.Name, Subtitle = $"{feature.Name} > {g.Header}", NavigationTarget = sub });
            }
        }

        foreach (var f in Features)
        {
            _allSearchableItems.Add(new SearchResultItem { Title = f.Name, Subtitle = "Feature", NavigationTarget = f });
            if (f.IsSystem) continue;
            AddItems(f, f.LeftColumnItems);
            AddItems(f, f.MiddleColumnItems);
            AddItems(f, f.RightColumnItems);
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
            .Take(10).ToList();
        SearchResults = [.. results];
        IsSearchPopupOpen = results.Any();
    }

    [RelayCommand]
    public void NavigateToSearchResult(SearchResultItem result)
    {
        if (result == null) return;
        if (result.NavigationTarget is FeatureViewModel feature)
            SelectedFeature = feature;
        else if (result.NavigationTarget is ItemViewModel item)
        {
            foreach (var f in Features)
            {
                ObservableCollection<object>[] cols = [f.LeftColumnItems, f.MiddleColumnItems, f.RightColumnItems];
                var allItems = cols
                    .SelectMany(col => col)
                    .SelectMany(i => i is ItemGroupViewModel g ? g.Items.Cast<object>() : [(object)i]);

                if (allItems.Contains(item)) { SelectedFeature = f; break; }
            }
        }
        SearchText = "";
        SearchResults.Clear();
        IsSearchPopupOpen = false;
    }

    private async Task CreateBackupOnFirstRun()
    {
        try
        {
            string taskPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane", "Cleaner");
            Directory.CreateDirectory(taskPath);

            const string backupName = "Winsane First Run Backup";
            if (await _coreService.RestorePointExistsAsync(backupName)) return;

            BusyMessage = "Creating Restore Point...";
            await _coreService.CreateSystemRestorePointAsync(backupName);
        }
        catch { }
    }
}
