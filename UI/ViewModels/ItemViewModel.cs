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

    // UI Logic:
    // If it's reversible (has FalseCommand), show toggle.
    // If it's one-way (App install or Script), show Run button.
    public bool ShowToggle => !ShowRunButton;
    public bool ShowRunButton => !_item.IsIrreversible == false && !HasSubItems;
    public bool ShowDeleteButton => IsUserTweak;
    
    public event Action<ItemViewModel>? OnDeleted;
    
    public ItemViewModel(Item item, CoreService coreService, ConfigService configService)
    {
        _item = item;
        _coreService = coreService;
        _configService = configService;
        
        _name = item.Name;
        _purpose = item.Purpose;
        _isHeader = item.IsCategory;
        _categoryName = item.Category;
        _isUserTweak = item.IsUserTweak;

        // Recursively load subitems
        if (item.SubItems.Any())
        {
            HasSubItems = true;
            foreach(var subItem in item.SubItems)
            {
                var subVm = new ItemViewModel(subItem, _coreService, _configService);
                subVm.PropertyChanged += SubItem_PropertyChanged;
                SubItems.Add(subVm);
            }
        }
    }
    
    private bool _suppressPropagation;
    private bool _isInitialized;
    private bool _suppressCommand;

    private void SubItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsEnabled) && !_suppressPropagation)
        {
            // Simple logic: if any child is on, parent status reflects that (visual only)
            _suppressPropagation = true;
            IsEnabled = SubItems.Any(x => x.IsEnabled);
            _suppressPropagation = false;
        }
    }
    
    partial void OnIsEnabledChanged(bool value)
    {
        if (!_isInitialized || _suppressCommand || _suppressPropagation) return;
        
        // Propagate to children for "Batch" operations
        if (HasSubItems && !IsLoading)
        {
            _suppressPropagation = true;
            foreach(var sub in SubItems)
            {
                if (sub.IsEnabled != value) sub.IsEnabled = value;
            }
            _suppressPropagation = false;
            return;
        }

        ExecuteToggleAsync(value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await CheckStateAsync();
    }

    public async Task CheckStateAsync()
    {
        if (IsHeader || string.IsNullOrEmpty(_item.CheckCommand)) return;

        try 
        {
            // We don't need FeatureName anymore, the pool handles it
            var (success, output, _) = await _coreService.ExecutePowerShellAsync(_item.CheckCommand);
            
            // PowerShell typically returns "True" or "False" string for boolean checks
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
        catch 
        { 
            // Silent fail on checks is acceptable
        }
    }
    
    private async Task ExecuteToggleAsync(bool isOn)
    {
        var command = isOn ? _item.TrueCommand : _item.FalseCommand;
        if (string.IsNullOrEmpty(command)) return;

        IsLoading = true;
        StatusText = isOn ? "Working..." : "Reverting...";
        
        try
        {
            var (success, output, error) = await _coreService.ExecutePowerShellAsync(command);
            StatusText = success ? "Done" : "Failed";
            
            if (!success)
            {
                // Revert toggle visually if failed
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
        if (IsLoading || string.IsNullOrEmpty(_item.TrueCommand)) return;
        
        IsLoading = true;
        StatusText = "Executing...";
        
        try
        {
            var (success, _, error) = await _coreService.ExecutePowerShellAsync(_item.TrueCommand);
            StatusText = success ? "Done" : "Failed";
            
            // Trigger a re-check if we just ran an installation
            if (success) await CheckStateAsync();
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
        if (IsLoading || !IsUserTweak) return;
        
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