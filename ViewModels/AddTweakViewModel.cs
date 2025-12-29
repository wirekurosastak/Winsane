using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinsaneCS.Models;
using WinsaneCS.Services;

namespace WinsaneCS.ViewModels;

/// <summary>
/// ViewModel for the Add Tweak form in the User category
/// </summary>
public partial class AddTweakViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly ObservableCollection<ItemViewModel> _parentItems;
    private readonly PowerShellService _powerShellService;
    private readonly WingetService _wingetService;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _purpose = string.Empty;
    
    [ObservableProperty]
    private string _trueCommand = string.Empty;
    
    [ObservableProperty]
    private string _falseCommand = string.Empty;
    
    [ObservableProperty]
    private string _errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool _isAdding;
    
    public AddTweakViewModel(
        AppConfig config, 
        ConfigService configService, 
        PowerShellService powerShellService,
        WingetService wingetService,
        ObservableCollection<ItemViewModel> parentItems)
    {
        _config = config;
        _configService = configService;
        _powerShellService = powerShellService;
        _wingetService = wingetService;
        _parentItems = parentItems;
    }
    
    [RelayCommand]
    private async Task AddTweak()
    {
        ErrorMessage = string.Empty;
        
        // Validate
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }
        
        if (string.IsNullOrWhiteSpace(TrueCommand))
        {
            ErrorMessage = "PowerShell ON command is required.";
            return;
        }
        
        if (string.IsNullOrWhiteSpace(FalseCommand))
        {
            ErrorMessage = "PowerShell OFF command is required.";
            return;
        }
        
        IsAdding = true;
        
        try
        {
            var newItem = await _configService.AddUserTweakAsync(_config, Name, Purpose, TrueCommand, FalseCommand);
            
            if (newItem != null)
            {
                // Create ViewModel for the new item
                var itemVm = new ItemViewModel(newItem, _powerShellService, _wingetService, _configService)
                {
                    IsUserTweak = true
                };
                
                _parentItems.Add(itemVm);
                
                // Clear form
                Name = string.Empty;
                Purpose = string.Empty;
                TrueCommand = string.Empty;
                FalseCommand = string.Empty;
            }
            else
            {
                ErrorMessage = "Failed to add tweak.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsAdding = false;
        }
    }
}
