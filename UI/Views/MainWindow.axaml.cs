using Avalonia.Controls;
using Avalonia.Input;
using FluentAvalonia.UI.Controls;
using Winsane.UI.ViewModels;
using Winsane.Infrastructure.Services;

namespace Winsane.UI.Views;

public partial class MainWindow : Window
{
    private readonly SystemInfoService _systemInfoService = new();
    
    public MainWindow()
    {
        InitializeComponent();
        
        
        Loaded += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm && vm.Features.Count > 0)
            {
                vm.SelectedFeature = vm.Features[0];
                
                
                if (vm.SelectedFeature.IsSystem)
                {
                    vm.SelectedFeature.RefreshSystem(_systemInfoService);
                }
            }
        };
    }
    
    private void NavView_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.IsSettingsInvoked)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsSettingsVisible = true;
            }
            return;
        }
    }
    
    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        
        if (e.IsSettingsSelected)
        {
            vm.IsSettingsVisible = true;
            return;
        }
        
        vm.IsSettingsVisible = false;
        
        if (e.SelectedItem is FeatureViewModel feature)
        {
            vm.SelectedFeature = feature;
            
            if (feature.IsSystem)
            {
                feature.RefreshSystem(_systemInfoService);
            }
        }
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var navView = this.FindControl<NavigationView>("NavView");
            if (navView != null)
            {
                navView.IsPaneOpen = true;
            }

            var searchBox = this.FindControl<TextBox>("SearchBox");
            if (searchBox != null)
            {
                searchBox.Text = string.Empty;
                searchBox.Focus();
            }
            e.Handled = true;
        }
    }
}
