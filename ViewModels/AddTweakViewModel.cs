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
    private readonly PowerShellService _powerShellService;
    private readonly WingetService _wingetService;
    
    // The list that the SettingsExpander binds to.
    // Contains [this (The Form), Tweak1, Tweak2, ...]
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
        ConfigService configService, 
        PowerShellService powerShellService,
        WingetService wingetService,
        List<ItemViewModel> existingTweaks)
    {
        _config = config;
        _configService = configService;
        _powerShellService = powerShellService;
        _wingetService = wingetService;
        
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
        
        // ConfigService handles file persistence via the ItemViewModel's Delete command calling ConfigService?
        // Wait, ItemViewModel calls OnDeleted. FeatureViewModel usually handled the actual ConfigService delete call.
        // We need to handle it here now, OR ensure ItemViewModel does it.
        // ItemViewModel.Delete() calls OnDeleted.
        // It does NOT call ConfigService directly for deletion logic usually?
        // Let's check ItemViewModel.cs again.
        // ItemViewModel.Delete() just invokes OnDeleted event.
        // So WE must perform the deletion from config here.
        DeleteTweakFromConfig(tweak);
    }
    
    private async void DeleteTweakFromConfig(ItemViewModel tweak)
    {
         await _configService.DeleteUserTweakAsync(_config, tweak.Name);
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
