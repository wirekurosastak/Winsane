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
        int itemsPerCol = (int)Math.Ceiling(items.Count / 3.0);

        var left = items.Take(itemsPerCol).ToList();
        var middle = items.Skip(itemsPerCol).Take(itemsPerCol).ToList();
        var right = items.Skip(itemsPerCol * 2).ToList();

        LeftColumnItems = new ObservableCollection<object>(left);
        MiddleColumnItems = new ObservableCollection<object>(middle);
        RightColumnItems = new ObservableCollection<object>(right);
    }

    public bool IsGenericFeature => !IsSystem;
    
    public void RefreshSystem(SystemInfoService systemInfoService)
    {
        System?.Refresh(systemInfoService);
    }
}
