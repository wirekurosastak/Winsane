using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winsane.Core.Interfaces;
using Winsane.Core.Models;

namespace Winsane.UI.ViewModels;

/// <summary>
/// ViewModel for the Add Tweak form in the User category
/// </summary>
public partial class AddTweakViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly AppConfig _config;
    private readonly ICoreService _coreService;
    
    // The list that the SettingsExpander binds to.
    public ObservableCollection<object> CombinedItems { get; } = new();
    
    private readonly ObservableCollection<ItemViewModel> _userTweaks = new();
    
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
        IConfigService configService, 
        ICoreService coreService,
        List<ItemViewModel> existingTweaks)
    {
        _config = config;
        _configService = configService;
        _coreService = coreService;
        
        // 1. Add Self (The Form)
        CombinedItems.Add(this);
        
        // 2. Add Existing Tweaks
        foreach(var tweak in existingTweaks)
        {
            RegisterTweak(tweak);
        }
    }
    
    private void RegisterTweak(ItemViewModel tweak)
    {
        tweak.OnDeleted += OnTweakDeleted;
        _userTweaks.Add(tweak);
        CombinedItems.Add(tweak);
    }
    
    private void OnTweakDeleted(ItemViewModel tweak)
    {
        tweak.OnDeleted -= OnTweakDeleted;
        _userTweaks.Remove(tweak);
        CombinedItems.Remove(tweak);
        
        DeleteTweakFromConfig(tweak);
    }
    
    private void DeleteTweakFromConfig(ItemViewModel tweak)
    {
        // Fire and forget - deletion errors are non-critical
        _ = _configService.DeleteUserTweakAsync(_config, tweak.Name);
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
                var itemVm = new ItemViewModel(newItem, _coreService, _configService)
                {
                    IsUserTweak = true
                };
                
                RegisterTweak(itemVm);
                
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
