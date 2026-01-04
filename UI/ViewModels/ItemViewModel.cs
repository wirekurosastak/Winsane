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
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _purpose = string.Empty;
    
    [ObservableProperty]
    private string _categoryName = string.Empty;
    
    [ObservableProperty]
    private bool _isHeader;
    
    [ObservableProperty]
    private bool _isEnabled;
    
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _buttonText = "Run";
    
    // Computed: show toggle only when run button is not shown
    public bool ShowToggle => !ShowRunButton;
    
    // Computed: show run button for irreversible items (unless it's an app)
    public bool ShowRunButton => !IsAppsFeature && !HasSubItems && _item.IsIrreversible;
    
    [ObservableProperty]
    private bool _isAppsFeature;
    
    [ObservableProperty]
    private bool _isUserTweak;
    
    // Computed: show delete button only for user tweaks
    public bool ShowDeleteButton => IsUserTweak;
    
    [ObservableProperty]
    private string _statusText = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ItemViewModel> _subItems = new();
    
    [ObservableProperty]
    private bool _hasSubItems;
    
    public event Action<ItemViewModel>? OnDeleted;
    
    public ItemViewModel(
        Item item, 
        CoreService coreService, 
        ConfigService configService)
    {
        _item = item;
        _coreService = coreService;
        _configService = configService;
        
        Name = item.Name ?? string.Empty;
        Purpose = item.Purpose ?? string.Empty;
        IsHeader = item.IsCategory;
        CategoryName = item.Category ?? string.Empty;  // Expose category text for headers
        // IsEnabled is strictly determined by CheckStateAsync
        
        // Load subitems
        if (item.SubItems != null && item.SubItems.Any())
        {
            HasSubItems = true;
            foreach(var subItem in item.SubItems)
            {
                var subVm = new ItemViewModel(subItem, _coreService, _configService);
                subVm.PropertyChanged += SubItem_PropertyChanged;
                SubItems.Add(subVm);
            }
        }
        
        _isInitialized = true;
        _ = CheckStateAsync();
    }
    
    private bool _suppressPropagation;
    private bool _isMassUpdating;
    private bool _isInitialized;
    private bool _suppressCommand;
    
    private void SubItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isMassUpdating) return;

        if (e.PropertyName == nameof(IsEnabled))
        {
            bool anyEnabled = SubItems.Any(x => x.IsEnabled);
            if (IsEnabled != anyEnabled)
            {
                _suppressPropagation = true;
                IsEnabled = anyEnabled;
                _suppressPropagation = false;
            }
        }
    }
    
    partial void OnIsEnabledChanged(bool value)
    {
        if (_item == null) return;
        
        // _item.Enabled property removed. State is transient or managed by system.
        
        if (!_isInitialized || _suppressCommand) return;
        
        if (_suppressPropagation) return;
        
        if (HasSubItems && !IsLoading)
        {
            _isMassUpdating = true;
            foreach(var sub in SubItems)
            {
                if (sub.IsEnabled != value)
                {
                    sub.IsEnabled = value;
                }
            }
            _isMassUpdating = false;
            return;
        }

        ExecuteToggleAsync(value);
    }

    public async Task CheckStateAsync()
    {
        // 1. Check Winget App
        if (_item.PackageId != null)
        {
            try
            {
                bool installed = await _coreService.IsWingetInstalledAsync(_item.PackageId);
                if (IsEnabled != installed)
                {
                    _suppressCommand = true;
                    IsEnabled = installed;
                    _suppressCommand = false;
                }
            }
            catch {}
            return;
        }

        // 2. Check PowerShell Tweak
        // Run check silently
        try 
        {
            var (success, output, _) = await _coreService.ExecutePowerShellAsync(_item.CheckCommand);
            // PowerShell returns "True" or "False" with newlines usually
            var cleanOutput = output?.Trim();
            
            if (success && bool.TryParse(cleanOutput, out bool result))
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
            // Check failed, keep existing state
        }
    }

    partial void OnIsAppsFeatureChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRunButton));
        OnPropertyChanged(nameof(ShowToggle));
    }
    
    private void ExecuteToggleAsync(bool isOn)
    {
        // Fire and forget with proper exception handling
        _ = ExecuteToggleCoreAsync(isOn);
    }
    
    private async Task ExecuteToggleCoreAsync(bool isOn)
    {
        if (IsLoading) return;
        
        if (_item.PackageId != null)
        {
            IsLoading = true;
            StatusText = isOn ? "Installing..." : "Uninstalling...";
            
            try
            {
                await _coreService.QueueWingetTaskAsync(_item.PackageId, isOn, Name);
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                _ = ClearStatusAfterDelay();
            }
            return;
        }
        
        var command = isOn ? _item.TrueCommand : _item.FalseCommand;
        
        IsLoading = true;
        StatusText = isOn ? "Enabling..." : "Disabling...";
        
        try
        {
            var (success, output, error) = await _coreService.ExecutePowerShellAsync(command);
            StatusText = success ? "Done" : $"Error: {error}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
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
        if (IsLoading) return;
        
        var command = _item.TrueCommand;
        
        IsLoading = true;
        StatusText = "Running...";
        
        try
        {
            // Simplified: All commands, including Start-Process, are run via PowerShell.
            var (success, output, error) = await _coreService.ExecutePowerShellAsync(command);
            StatusText = success ? "Done" : $"Error: {error}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _ = ClearStatusAfterDelay();
        }
    }

    private async Task ClearStatusAfterDelay()
    {
        await Task.Delay(2000);
        StatusText = string.Empty;
    }
    
    [RelayCommand]
    private async Task Delete()
    {
        if (IsLoading || !IsUserTweak) return;
        
        IsLoading = true;
        StatusText = "Deleting...";
        
        try
        {
            OnDeleted?.Invoke(this);
            StatusText = "Deleted";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _ = ClearStatusAfterDelay();
        }
    }
}
