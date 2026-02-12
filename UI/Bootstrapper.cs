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

        services.AddSingleton<ConfigService>();
        services.AddSingleton<CoreService>();
        services.AddSingleton<SystemInfoService>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SystemViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public static MainWindowViewModel CreateMainViewModel()
    {
        if (_serviceProvider == null)
            Initialize();
        return _serviceProvider!.GetRequiredService<MainWindowViewModel>();
    }

    public static T GetService<T>()
        where T : notnull
    {
        if (_serviceProvider == null)
            Initialize();
        return _serviceProvider!.GetRequiredService<T>();
    }
}
