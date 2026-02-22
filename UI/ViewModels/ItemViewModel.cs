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
    private readonly bool _isSubItem;
    private bool _suppressPropagation, _isInitialized, _suppressCommand;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _purpose;
    [ObservableProperty] private string _categoryName;
    [ObservableProperty] private bool _isHeader;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _buttonText = "Run";
    [ObservableProperty] private bool _isUserTweak;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ObservableCollection<ItemViewModel> _subItems = [];
    [ObservableProperty] private bool _hasSubItems;
    [ObservableProperty] private string? _icon;

    public bool ShowToggle => HasCommands || (HasSubItems && _isSubItem);
    private bool HasCommands => !string.IsNullOrEmpty(_item.TrueCommand) || !string.IsNullOrEmpty(_item.FalseCommand);
    public bool ShowRunButton => !string.IsNullOrEmpty(_item.ButtonCommand);
    public bool ShowDeleteButton => IsUserTweak;
    public bool IsInstaller => _lane == CoreService.PowerShellLane.Installer;

    public event Action<ItemViewModel>? OnDeleted;

    public ItemViewModel(
        Item item, CoreService coreService, ConfigService configService,
        bool isInstaller = false, bool isSubItem = false, bool isUserTweak = false)
    {
        _item = item;
        _coreService = coreService;
        _configService = configService;
        _isSubItem = isSubItem;
        _lane = isInstaller ? CoreService.PowerShellLane.Installer : CoreService.PowerShellLane.General;
        _name = item.Name;
        _purpose = item.Purpose;
        _isHeader = item.IsCategory;
        _categoryName = item.Category ?? string.Empty;
        _isUserTweak = isUserTweak;
        _icon = item.Icon;
        _buttonText = !string.IsNullOrEmpty(item.ButtonText) ? item.ButtonText : "Run";

        if (item.SubItems?.Any() == true)
        {
            HasSubItems = true;
            foreach (var sub in item.SubItems)
            {
                var subVm = new ItemViewModel(sub, _coreService, _configService, isInstaller, true);
                subVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IsEnabled) && !_suppressPropagation)
                    {
                        _suppressPropagation = true;
                        IsEnabled = SubItems.Any(x => x.IsEnabled);
                        _suppressPropagation = false;
                    }
                };
                SubItems.Add(subVm);
            }
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (!_isInitialized || _suppressCommand || _suppressPropagation) return;

        if (HasSubItems && !IsLoading)
        {
            _suppressPropagation = true;
            foreach (var sub in SubItems.Where(s => s.IsEnabled != value)) sub.IsEnabled = value;
            _suppressPropagation = false;
            return;
        }
        _ = ExecuteToggleAsync(value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        List<Task> tasks = [CheckStateAsync()];
        if (HasSubItems) tasks.AddRange(SubItems.Select(x => x.InitializeAsync()));
        await Task.WhenAll(tasks);
    }

    public async Task CheckStateAsync()
    {
        if (IsHeader || string.IsNullOrEmpty(_item.CheckCommand)) return;
        try
        {
            var (success, output, _) = await _coreService.ExecutePowerShellAsync(
                _item.CheckCommand, CoreService.PowerShellLane.General);
            if (success && bool.TryParse(output.Trim(), out bool result) && IsEnabled != result)
                SetSuppressed(result);
        }
        catch { }
    }

    private async Task ExecuteToggleAsync(bool isOn)
    {
        var command = isOn ? _item.TrueCommand : _item.FalseCommand;
        if (string.IsNullOrEmpty(command)) return;

        await RunWithStatus(
            IsInstaller ? (isOn ? "Installing..." : "Uninstalling...") : (isOn ? "Working..." : "Reverting..."),
            async () =>
            {
                var (success, _, _) = await _coreService.ExecutePowerShellAsync(command, _lane);
                StatusText = success ? "Done" : "Failed";
                if (!success) SetSuppressed(!isOn);
            },
            () => SetSuppressed(!isOn));
    }

    [RelayCommand]
    private async Task ExecuteButton()
    {
        if (IsLoading || string.IsNullOrEmpty(_item.ButtonCommand)) return;
        await RunWithStatus("Executing...", async () =>
        {
            var (success, _, _) = await _coreService.ExecutePowerShellAsync(_item.ButtonCommand, _lane);
            StatusText = success ? "Done" : "Failed";
            if (success) await CheckStateAsync();
        });
    }

    [RelayCommand]
    private void Delete()
    {
        if (IsLoading || !IsUserTweak) return;
        IsLoading = true;
        OnDeleted?.Invoke(this);
        IsLoading = false;
    }

    private async Task RunWithStatus(string status, Func<Task> action, Action? onError = null)
    {
        IsLoading = true;
        StatusText = status;
        try { await action(); }
        catch
        {
            StatusText = "Error";
            onError?.Invoke();
        }
        finally
        {
            IsLoading = false;
            _ = ClearStatusAfterDelay();
        }
    }

    private void SetSuppressed(bool value)
    {
        _suppressCommand = true;
        IsEnabled = value;
        _suppressCommand = false;
    }

    private async Task ClearStatusAfterDelay()
    {
        await Task.Delay(2000);
        StatusText = string.Empty;
    }
}
