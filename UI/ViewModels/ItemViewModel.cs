using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class ItemViewModel : ViewModelBase
{
    private readonly Item _item;
    private readonly CoreService _coreService;
    private readonly ConfigService _configService;
    private readonly CoreService.PowerShellLane _lane;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _purpose;

    [ObservableProperty]
    private string _categoryName;

    [ObservableProperty]
    private bool _isHeader;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _buttonText = "Run";

    [ObservableProperty]
    private bool _isUserTweak;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ItemViewModel> _subItems = new();

    [ObservableProperty]
    private bool _hasSubItems;

    [ObservableProperty]
    private string? _icon;

    public bool ShowToggle => HasCommands || (HasSubItems && _isSubItem);
    
    private bool HasCommands => !string.IsNullOrEmpty(_item.TrueCommand) || !string.IsNullOrEmpty(_item.FalseCommand);

    public bool ShowRunButton => !string.IsNullOrEmpty(_item.ButtonCommand);
    public bool ShowDeleteButton => IsUserTweak;
    public bool IsInstaller => _lane == CoreService.PowerShellLane.Installer;

    public event Action<ItemViewModel>? OnDeleted;

    private readonly bool _isSubItem;

    public ItemViewModel(
        Item item,
        CoreService coreService,
        ConfigService configService,
        bool isInstaller = false,
        bool isSubItem = false
    )
    {
        _item = item;
        _coreService = coreService;
        _configService = configService;
        _isSubItem = isSubItem;
        _lane = isInstaller
            ? CoreService.PowerShellLane.Installer
            : CoreService.PowerShellLane.General;

        _name = item.Name;
        _purpose = item.Purpose;
        _isHeader = item.IsCategory;
        _categoryName = item.Category ?? string.Empty;
        _isUserTweak = item.IsUserTweak;
        _icon = item.Icon;
        _buttonText = !string.IsNullOrEmpty(item.ButtonText) ? item.ButtonText : "Run";

        if (item.SubItems?.Any() == true)
        {
            HasSubItems = true;
            foreach (var subItem in item.SubItems)
            {
                var subVm = new ItemViewModel(subItem, _coreService, _configService, isInstaller, true);
                subVm.PropertyChanged += SubItem_PropertyChanged;
                SubItems.Add(subVm);
            }
        }
    }

    private bool _suppressPropagation;
    private bool _isInitialized;
    private bool _suppressCommand;

    private void SubItem_PropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e
    )
    {
        if (e.PropertyName == nameof(IsEnabled) && !_suppressPropagation)
        {
            _suppressPropagation = true;
            IsEnabled = SubItems.Any(x => x.IsEnabled);
            _suppressPropagation = false;
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (!_isInitialized || _suppressCommand || _suppressPropagation)
            return;

        if (HasSubItems && !IsLoading)
        {
            _suppressPropagation = true;
            foreach (var sub in SubItems)
            {
                if (sub.IsEnabled != value)
                    sub.IsEnabled = value;
            }
            _suppressPropagation = false;
            return;
        }

        _ = ExecuteToggleAsync(value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;
        _isInitialized = true;

        var tasks = new List<Task> { CheckStateAsync() };

        if (HasSubItems)
        {
            tasks.AddRange(SubItems.Select(x => x.InitializeAsync()));
        }

        await Task.WhenAll(tasks);
    }

    public async Task CheckStateAsync()
    {
        if (IsHeader || string.IsNullOrEmpty(_item.CheckCommand))
            return;

        try
        {
            var (success, output, _) = await _coreService.ExecutePowerShellAsync(
                _item.CheckCommand,
                CoreService.PowerShellLane.General
            );

            if (success && bool.TryParse(output.Trim(), out bool result))
            {
                if (IsEnabled != result)
                {
                    _suppressCommand = true;
                    IsEnabled = result;
                    _suppressCommand = false;
                }
            }
        }
        catch { }
    }

    private async Task ExecuteToggleAsync(bool isOn)
    {
        var command = isOn ? _item.TrueCommand : _item.FalseCommand;
        if (string.IsNullOrEmpty(command))
            return;

        IsLoading = true;
        if (IsInstaller)
            StatusText = isOn ? "Installing..." : "Uninstalling...";
        else
            StatusText = isOn ? "Working..." : "Reverting...";

        try
        {
            var (success, output, error) = await _coreService.ExecutePowerShellAsync(
                command,
                _lane
            );
            StatusText = success ? "Done" : "Failed";

            if (!success)
            {
                _suppressCommand = true;
                IsEnabled = !isOn;
                _suppressCommand = false;
            }
        }
        catch
        {
            StatusText = "Error";
            _suppressCommand = true;
            IsEnabled = !isOn;
            _suppressCommand = false;
        }
        finally
        {
            IsLoading = false;
            _ = ClearStatusAfterDelay();
        }
    }

    [RelayCommand]
    private async Task ExecuteButton()
    {
        if (IsLoading || string.IsNullOrEmpty(_item.ButtonCommand))
            return;

        IsLoading = true;
        StatusText = "Executing...";

        try
        {
            var (success, _, error) = await _coreService.ExecutePowerShellAsync(
                _item.ButtonCommand,
                _lane
            );
            StatusText = success ? "Done" : "Failed";

            if (success)
                await CheckStateAsync();
        }
        catch
        {
            StatusText = "Error";
        }
        finally
        {
            IsLoading = false;
            _ = ClearStatusAfterDelay();
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (IsLoading || !IsUserTweak)
            return;

        IsLoading = true;
        OnDeleted?.Invoke(this);
        IsLoading = false;
    }

    private async Task ClearStatusAfterDelay()
    {
        await Task.Delay(2000);
        StatusText = string.Empty;
    }
}
