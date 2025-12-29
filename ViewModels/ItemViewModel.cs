using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinsaneCS.Models;
using WinsaneCS.Services;

namespace WinsaneCS.ViewModels;

public partial class ItemViewModel : ViewModelBase
{
    private readonly Item _item;
    private readonly PowerShellService _powerShellService;
    private readonly WingetService _wingetService;
    private readonly ConfigService _configService;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _purpose = string.Empty;
    
    [ObservableProperty]
    private string? _header;
    
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
    
    // Event to notify parent when this item is deleted
    public event Action<ItemViewModel>? OnDeleted;
    
    public ItemViewModel(Item item, PowerShellService powerShellService, WingetService wingetService, ConfigService configService)
    {
        _item = item;
        _powerShellService = powerShellService;
        _wingetService = wingetService;
        _configService = configService;
        
        Name = item.Name ?? string.Empty;
        Purpose = item.Purpose ?? string.Empty;
        Header = item.Header;
        IsHeader = item.IsHeader;
        IsEnabled = item.Enabled;
        
        // Load subitems
        if (item.SubItems != null && item.SubItems.Any())
        {
            HasSubItems = true;
            foreach(var subItem in item.SubItems)
            {
                var subVm = new ItemViewModel(subItem, powerShellService, wingetService, configService);
                subVm.PropertyChanged += SubItem_PropertyChanged;
                SubItems.Add(subVm);
            }
        }
    }
    
    private bool _suppressPropagation;
    private bool _isMassUpdating;
    
    private void SubItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isMassUpdating) return;

        if (e.PropertyName == nameof(IsEnabled))
        {
            // Update parent based on children state
            // Parent is True if ANY child is True (Partial or Full)
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
        // Don't execute during initialization
        if (_item == null) return;
        
        // Update the model
        _item.Enabled = value;
        
        if (_suppressPropagation) return;
        
        // If this is a parent item (Auto Cleanup), enable/disable all children
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
            return; // Parent doesn't execute its own command if it's just a container
        }

        // Execute the appropriate command
        ExecuteToggleAsync(value);
    }

    partial void OnIsAppsFeatureChanged(bool value)
    {
        // Notify dependent properties when IsAppsFeature is set
        OnPropertyChanged(nameof(ShowRunButton));
        OnPropertyChanged(nameof(ShowToggle));
    }
    
    private async void ExecuteToggleAsync(bool isOn)
    {
        if (IsLoading) return;
        
        // Winget App Logic
        if (!string.IsNullOrEmpty(_item.PackageId))
        {
            IsLoading = true;
            StatusText = isOn ? "Installing..." : "Uninstalling...";
            
            try
            {
                await _wingetService.QueueTaskAsync(_item.PackageId, isOn, Name);
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                await Task.Delay(2000);
                StatusText = string.Empty;
            }
            return;
        }
        
        // PowerShell Logic
        var command = isOn ? _item.TrueCommand : _item.FalseCommand;
        if (string.IsNullOrEmpty(command)) return;
        
        IsLoading = true;
        StatusText = isOn ? "Enabling..." : "Disabling...";
        
        try
        {
            // Use PowerShell for other commands
            var (success, output, error) = await _powerShellService.ExecuteAsync(command);
            StatusText = success ? "Done" : $"Error: {error}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await Task.Delay(2000);
            StatusText = string.Empty;
        }
    }
    
    [RelayCommand]
    private async Task ExecuteButton()
    {
        if (IsLoading) return;
        
        var command = _item.TrueCommand;
        if (string.IsNullOrEmpty(command)) return;
        
        IsLoading = true;
        StatusText = "Running...";
        
        try
        {
            // Admin Tools or Irreversible actions
            if (command.StartsWith("Start-Process", StringComparison.OrdinalIgnoreCase))
            {
                _powerShellService.StartProcess(command);
                StatusText = "Opened";
            }
            else
            {
                var (success, output, error) = await _powerShellService.ExecuteAsync(command);
                StatusText = success ? "Done" : $"Error: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            await Task.Delay(2000);
            StatusText = string.Empty;
        }
    }
    
    private static string? ExtractPackageId(string command)
    {
        // Deprecated: Logic moved to data.yaml
        return null; 
    }
    
    [RelayCommand]
    private async Task Delete()
    {
        if (IsLoading || !IsUserTweak) return;
        
        // TODO: In a real app, show confirmation dialog
        // For now, just delete
        IsLoading = true;
        StatusText = "Deleting...";
        
        try
        {
            // Notify parent to remove this item
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
        }
    }
}
