using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class FeatureViewModel : ViewModelBase
{
    private readonly CoreService _coreService;
    private readonly ConfigService _configService;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private ObservableCollection<object> _leftColumnItems = new();
    [ObservableProperty] private ObservableCollection<object> _middleColumnItems = new();
    [ObservableProperty] private ObservableCollection<object> _rightColumnItems = new();
    [ObservableProperty] private bool _isSystem;
    [ObservableProperty] private SystemViewModel? _system;

    public Task InitializationTask { get; private set; } = Task.CompletedTask;
    public bool IsGenericFeature => !IsSystem;

    public FeatureViewModel(
        Feature feature, CoreService coreService,
        ConfigService configService, AppConfig? config = null)
    {
        _coreService = coreService;
        _configService = configService;
        _name = feature.Name;
        _icon = feature.Icon ?? "Settings";
        _isSystem = feature.Type?.Equals("system", StringComparison.OrdinalIgnoreCase) ?? false;

        if (_isSystem)
            System = new SystemViewModel();
        else
            InitializeItems(feature, config);
    }

    private void InitializeItems(Feature feature, AppConfig? config)
    {
        var rawItems = new List<object>();
        var initTasks = new List<Task>();
        if (feature.Items == null) { DistributeItems(rawItems); return; }

        bool isAppFeature = feature.Name.Equals("Apps", StringComparison.OrdinalIgnoreCase);
        int i = 0;

        while (i < feature.Items.Count)
        {
            var item = feature.Items[i];
            var itemVm = new ItemViewModel(item, _coreService, _configService, isAppFeature);
            initTasks.Add(itemVm.InitializeAsync());

            if (item.IsCategory)
            {
                if (item.Category == "Add Custom Tweak" && config != null)
                {
                    var existingUserTweaks = feature.UserTweaks
                        .Where(x => x.IsUserTweak)
                        .Select(x =>
                        {
                            var vm = new ItemViewModel(x, _coreService, _configService, false);
                            initTasks.Add(vm.InitializeAsync());
                            return vm;
                        }).ToList();

                    rawItems.Add(new AddTweakViewModel(config, _configService, _coreService, existingUserTweaks, item));
                    i++;
                    continue;
                }

                var group = new ItemGroupViewModel(item.Category, item.Icon, item.Column);
                bool isDebloat = item.Category?.Contains("Debloat", StringComparison.OrdinalIgnoreCase) == true;
                bool groupInstallerLane = isAppFeature && !isDebloat;
                bool isStartupApps = item.Category?.Equals("Startup Apps", StringComparison.OrdinalIgnoreCase) == true;

                i++;
                while (i < feature.Items.Count && !feature.Items[i].IsCategory)
                {
                    var subVm = new ItemViewModel(feature.Items[i], _coreService, _configService, groupInstallerLane);
                    initTasks.Add(subVm.InitializeAsync());
                    group.Items.Add(subVm);
                    i++;
                }

                if (isStartupApps || group.Items.Any()) rawItems.Add(group);
                if (isStartupApps) initTasks.Add(PopulateStartupAppsAsync(group));
            }
            else
            {
                rawItems.Add(itemVm);
                i++;
            }
        }

        DistributeItems(rawItems);
        InitializationTask = Task.WhenAll(initTasks);
    }

    private void DistributeItems(List<object> items)
    {
        var columns = new List<object>[] { new(), new(), new() };
        var unassigned = new List<object>();

        foreach (var item in items)
        {
            int? col = (item as ItemGroupViewModel)?.Column ?? (item as AddTweakViewModel)?.Column;
            if (col is >= 0 and <= 2)
                columns[col.Value].Add(item);
            else
                unassigned.Add(item);
        }

        foreach (var item in unassigned)
        {
            int smallest = columns[0].Count <= columns[1].Count && columns[0].Count <= columns[2].Count ? 0
                         : columns[1].Count <= columns[2].Count ? 1 : 2;
            columns[smallest].Add(item);
        }

        LeftColumnItems = new ObservableCollection<object>(columns[0]);
        MiddleColumnItems = new ObservableCollection<object>(columns[1]);
        RightColumnItems = new ObservableCollection<object>(columns[2]);
    }

    public void RefreshSystem(SystemInfoService systemInfoService) => System?.Refresh(systemInfoService);

    private async Task PopulateStartupAppsAsync(ItemGroupViewModel group)
    {
        try
        {
            var entries = await new StartupService(_coreService).GetStartupEntriesAsync();
            var tasks = new List<Task>();
            foreach (var entry in entries)
            {
                var vm = new ItemViewModel(entry, _coreService, _configService, false);
                tasks.Add(vm.InitializeAsync());
                group.Items.Add(vm);
            }
            await Task.WhenAll(tasks);
        }
        catch { }
    }
}
