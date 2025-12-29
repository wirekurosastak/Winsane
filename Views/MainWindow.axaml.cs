using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using WinsaneCS.ViewModels;
using WinsaneCS.Services;

namespace WinsaneCS.Views;

public partial class MainWindow : Window
{
    private readonly SystemInfoService _systemInfoService = new();
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Select first item after loading
        Loaded += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm && vm.Features.Count > 0)
            {
                vm.SelectedFeature = vm.Features[0];
                
                // Refresh dashboard if first feature is Dashboard
                if (vm.SelectedFeature.IsDashboard)
                {
                    vm.SelectedFeature.RefreshDashboard(_systemInfoService);
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
            
            if (feature.IsDashboard)
            {
                feature.RefreshDashboard(_systemInfoService);
            }
        }
    }
}
