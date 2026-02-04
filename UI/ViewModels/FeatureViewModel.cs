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
    private ObservableCollection<object> _rightColumnItems = new();

    [ObservableProperty]
    private bool _isDashboard;

    [ObservableProperty]
    private DashboardViewModel? _dashboard;

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
        _isDashboard =
            feature.Type?.Equals("dashboard", StringComparison.OrdinalIgnoreCase) ?? false;

        if (_isDashboard)
        {
            Dashboard = new DashboardViewModel();
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
                bool isInstaller =
                    feature.Name.Equals("Installer", StringComparison.OrdinalIgnoreCase) == true;
                var itemVm = new ItemViewModel(item, _coreService, _configService, isInstaller);

                initTasks.Add(itemVm.InitializeAsync());

                if (item.IsCategory)
                {
                    var group = new ItemGroupViewModel(item.Category, item.Icon);

                    i++;
                    while (i < feature.Items.Count && !feature.Items[i].IsCategory)
                    {
                        var subItem = feature.Items[i];
                        var subItemVm = new ItemViewModel(
                            subItem,
                            _coreService,
                            _configService,
                            isInstaller
                        );
                        initTasks.Add(subItemVm.InitializeAsync());

                        group.Items.Add(subItemVm);
                        i++;
                    }

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
                    .Items?.Where(x => x.IsUserTweak)
                    .Select(x => new ItemViewModel(x, _coreService, _configService, false))
                    .ToList()
                ?? new List<ItemViewModel>();

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
        int splitIndex = (int)Math.Ceiling(items.Count / 2.0);

        var left = items.Take(splitIndex);
        var right = items.Skip(splitIndex);

        LeftColumnItems = new ObservableCollection<object>(left);
        RightColumnItems = new ObservableCollection<object>(right);
    }

    public void RefreshDashboard(SystemInfoService systemInfoService)
    {
        Dashboard?.Refresh(systemInfoService);
    }
}
