using Microsoft.Extensions.DependencyInjection;
using Winsane.Core.Interfaces;
using Winsane.Infrastructure.Services;
using Winsane.UI.ViewModels;

namespace Winsane.UI;

public static class Bootstrapper
{
    private static ServiceProvider? _serviceProvider;

    public static void Initialize()
    {
        var services = new ServiceCollection();

        // Register Services (Infrastructure)
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ICoreService, CoreService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        
        // Register ViewModels (UI)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>(); 
        
        // Note: FeatureViewModel and others might need registration or factory if they are created dynamically.
        // Currently FeatureViewModel is created manually in MainWindowViewModel, which is fine if dependencies are passed.
        // But better if we use a factory or DI.
        // For now, keeping existing pattern where MainWindowViewModel constructs them, 
        // OR we can rely on DI if we change MainWindowViewModel to use DI for features.
        // Existing code passed services manually. We will see in MainWindowViewModel.

        _serviceProvider = services.BuildServiceProvider();
    }

    public static MainWindowViewModel CreateMainViewModel()
    {
        if (_serviceProvider == null) Initialize();
        return _serviceProvider!.GetRequiredService<MainWindowViewModel>();
    }
    
    // Helper to resolve services manually if needed (e.g. for manually created VMs)
    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null) Initialize();
        return _serviceProvider!.GetRequiredService<T>();
    }
}
