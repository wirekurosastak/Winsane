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
    private readonly Item _categoryItem;

    public ObservableCollection<object> CombinedItems { get; } = new();
    private readonly ObservableCollection<ItemViewModel> _userTweaks = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _purpose = string.Empty;
    [ObservableProperty] private string _checkCommand = string.Empty;
    [ObservableProperty] private string _trueCommand = string.Empty;
    [ObservableProperty] private string _falseCommand = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isAdding;

    public string Category => _categoryItem.Category ?? "Add Custom Tweaks";
    public string? Icon => _categoryItem.Icon;
    public int? Column => _categoryItem.Column;

    public AddTweakViewModel(
        AppConfig config, ConfigService configService,
        CoreService coreService, List<ItemViewModel> existingTweaks, Item categoryItem)
    {
        _config = config;
        _configService = configService;
        _coreService = coreService;
        _categoryItem = categoryItem;

        CombinedItems.Add(this);
        foreach (var tweak in existingTweaks) RegisterTweak(tweak);
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
        _ = _configService.DeleteUserTweakAsync(_config, tweak.Name);
    }

    private string? Validate() =>
        string.IsNullOrWhiteSpace(Name) ? "Name is required." :
        string.IsNullOrWhiteSpace(CheckCommand) ? "Check command is required." :
        string.IsNullOrWhiteSpace(TrueCommand) ? "PowerShell ON command is required." :
        string.IsNullOrWhiteSpace(FalseCommand) ? "PowerShell OFF command is required." : null;

    [RelayCommand]
    private async Task AddTweak()
    {
        ErrorMessage = Validate() ?? string.Empty;
        if (ErrorMessage != string.Empty) return;

        IsAdding = true;
        try
        {
            var newItem = await _configService.AddUserTweakAsync(
                _config, Name, Purpose, TrueCommand, FalseCommand, CheckCommand);

            if (newItem != null)
            {
                var itemVm = new ItemViewModel(newItem, _coreService, _configService) { IsUserTweak = true };
                await itemVm.InitializeAsync();
                RegisterTweak(itemVm);
                Name = Purpose = TrueCommand = FalseCommand = CheckCommand = string.Empty;
            }
            else
            {
                ErrorMessage = "Failed to add tweak.";
            }
        }
        catch (Exception ex) { ErrorMessage = $"Error: {ex.Message}"; }
        finally { IsAdding = false; }
    }
}
