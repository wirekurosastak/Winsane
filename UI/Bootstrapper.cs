using Microsoft.Extensions.DependencyInjection;
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
        // Register Services (Infrastructure)
        services.AddSingleton<ConfigService>();
        services.AddSingleton<CoreService>();
        services.AddSingleton<SystemInfoService>();
        
        // Register ViewModels (UI)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>(); 
        
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
