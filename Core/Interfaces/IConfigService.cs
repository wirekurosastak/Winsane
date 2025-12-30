using Winsane.Core.Models;

namespace Winsane.Core.Interfaces;

public interface IConfigService
{
    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);
    Task<Item?> AddUserTweakAsync(AppConfig config, string name, string purpose, string trueCmd, string falseCmd);
    Task DeleteUserTweakAsync(AppConfig config, string name);
}
