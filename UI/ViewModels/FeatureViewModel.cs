using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class FeatureViewModel : ViewModelBase
{
    private readonly CoreService _coreService;
    private readonly ConfigService _configService;

    [ObservableProperty]
    private string _name;

    public Task InitializationTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    private string _icon;

    [ObservableProperty]
    private ObservableCollection<object> _leftColumnItems = new();

    [ObservableProperty]
    private ObservableCollection<object> _middleColumnItems = new();

    [ObservableProperty]
    private ObservableCollection<object> _rightColumnItems = new();

    [ObservableProperty]
    private bool _isSystem;

    [ObservableProperty]
    private SystemViewModel? _system;

    public FeatureViewModel(
        Feature feature,
        CoreService coreService,
        ConfigService configService,
        AppConfig? config = null
    )
    {
        _coreService = coreService;
        _configService = configService;

        _name = feature.Name;
        _icon = feature.Icon ?? "Settings";
        _isSystem =
            feature.Type?.Equals("system", StringComparison.OrdinalIgnoreCase) ?? false;

        if (_isSystem)
        {
            System = new SystemViewModel();
        }
        else
        {
            InitializeItems(feature, config);
        }
    }

    private void InitializeItems(Feature feature, AppConfig? config)
    {
        var rawItems = new List<object>();
        var initTasks = new List<Task>();

        if (feature.Items != null)
        {
            int i = 0;
            while (i < feature.Items.Count)
            {
                var item = feature.Items[i];

                bool isAppFeature = feature.Name.Equals("Apps", StringComparison.OrdinalIgnoreCase);

                // Default behavior: if it's the Apps/Installer feature, use the installer lane
                bool useInstallerLane = isAppFeature;

                var itemVm = new ItemViewModel(item, _coreService, _configService, useInstallerLane);

                initTasks.Add(itemVm.InitializeAsync());

                if (item.IsCategory)
                {
                    var group = new ItemGroupViewModel(item.Category, item.Icon, item.Column);
                    
                    // If this category is "Debloat", we MUST use the General lane, NOT the Installer lane
                    // This prevents Debloat actions from blocking/being blocked by long-running Winget installs
                    bool isDebloat = item.Category?.Contains("Debloat", StringComparison.OrdinalIgnoreCase) == true;
                    bool groupUseInstallerLane = useInstallerLane && !isDebloat;

                    i++;
                    while (i < feature.Items.Count && !feature.Items[i].IsCategory)
                    {
                        var subItem = feature.Items[i];
                        var subItemVm = new ItemViewModel(
                            subItem,
                            _coreService,
                            _configService,
                            groupUseInstallerLane
                        );
                        initTasks.Add(subItemVm.InitializeAsync());

                        group.Items.Add(subItemVm);
                        i++;
                    }

                    if (group.Items.Any())
                         rawItems.Add(group);
                }
                else
                {
                    rawItems.Add(itemVm);
                    i++;
                }
            }
        }

        if (!string.IsNullOrEmpty(feature.UserTweaksSection) && config != null)
        {
            var existingUserTweaks =
                feature
                    .UserTweaks.Where(x => x.IsUserTweak)
                    .Select(x => 
                    {
                        var vm = new ItemViewModel(x, _coreService, _configService, false);
                        initTasks.Add(vm.InitializeAsync());
                        return vm;
                    })
                    .ToList();

            var addTweakVm = new AddTweakViewModel(
                config,
                _configService,
                _coreService,
                existingUserTweaks
            );
            rawItems.Add(addTweakVm);
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
            int? col = (item as ItemGroupViewModel)?.Column;
            if (col.HasValue && col.Value >= 0 && col.Value <= 2)
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

    public bool IsGenericFeature => !IsSystem;
    
    public void RefreshSystem(SystemInfoService systemInfoService)
    {
        System?.Refresh(systemInfoService);
    }
}
