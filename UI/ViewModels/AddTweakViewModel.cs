using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winsane.Core.Models;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.ViewModels;

public partial class AddTweakViewModel : ViewModelBase
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly CoreService _coreService;

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
        CoreService coreService,
        List<ItemViewModel> existingTweaks
    )
    {
        _config = config;
        _configService = configService;
        _coreService = coreService;

        CombinedItems.Add(this);

        foreach (var tweak in existingTweaks)
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
        _ = _configService.DeleteUserTweakAsync(_config, tweak.Name);
    }

    [RelayCommand]
    private async Task AddTweak()
    {
        ErrorMessage = string.Empty;

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
            var newItem = await _configService.AddUserTweakAsync(
                _config,
                Name,
                Purpose,
                TrueCommand,
                FalseCommand
            );

            if (newItem != null)
            {
                var itemVm = new ItemViewModel(newItem, _coreService, _configService)
                {
                    IsUserTweak = true,
                };

                RegisterTweak(itemVm);

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
