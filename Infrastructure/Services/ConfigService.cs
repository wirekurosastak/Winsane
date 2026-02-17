
using Winsane.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Winsane.Infrastructure.Services;

public class ConfigService
{
    private static readonly string WinsaneFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane");
    private const string PrefsFileName = "user_tweaks.yaml";
    private const string RemoteConfigUrl =
        "https://raw.githubusercontent.com/wirekurosastak/Winsane/refs/heads/main/Assets/data.yaml";

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
        .Build();

    private readonly HttpClient _httpClient;

    public ConfigService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Winsane-Dev");
    }

    private string PrefsFilePath => Path.Combine(WinsaneFolder, PrefsFileName);

    public async Task<AppConfig> LoadConfigAsync()
    {
        var config = await FetchBaseConfigAsync();
        await ApplyUserPrefsAsync(config);
        return config;
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        Directory.CreateDirectory(WinsaneFolder);
        var prefs = new UserPrefs
        {
            Theme = config.Theme,
            UserTweaks = config.Features
                .SelectMany(f => f.UserTweaks)
                .ToList(),
        };
        await File.WriteAllTextAsync(PrefsFilePath, _yamlSerializer.Serialize(prefs));
    }

    private async Task<AppConfig> FetchBaseConfigAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(RemoteConfigUrl);
            if (response.IsSuccessStatusCode)
                return _yamlDeserializer.Deserialize<AppConfig>(await response.Content.ReadAsStringAsync());
        }
        catch { }

        var resourceName = "Winsane.Assets.data.yaml";
        using var stream = typeof(ConfigService).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Critical: Embedded config not found.");
        using var reader = new StreamReader(stream);
        return _yamlDeserializer.Deserialize<AppConfig>(await reader.ReadToEndAsync()) ?? new AppConfig();
    }

    private async Task ApplyUserPrefsAsync(AppConfig config)
    {
        if (!File.Exists(PrefsFilePath)) return;
        try
        {
            var prefs = _yamlDeserializer.Deserialize<UserPrefs>(await File.ReadAllTextAsync(PrefsFilePath));
            if (prefs == null) return;

            if (prefs.Theme != null) config.Theme = prefs.Theme;

            if (prefs.UserTweaks?.Any() != true) return;
            var target = FindUserTweakFeature(config);
            if (target == null) return;

            foreach (var tweak in prefs.UserTweaks.Where(t => !target.UserTweaks.Any(i => i.Name == t.Name)))
            {
                target.UserTweaks.Add(tweak);
            }
        }
        catch { }
    }

    private static Feature? FindUserTweakFeature(AppConfig config) =>
        config.Features.FirstOrDefault(f => f.Items.Any(i => i.Category == "Add Custom Tweak"));

    public async Task<Item?> AddUserTweakAsync(
        AppConfig config, string name, string purpose,
        string trueCmd, string falseCmd, string checkCmd)
    {
        var target = FindUserTweakFeature(config);
        if (target == null) return null;

        var newItem = new Item
        {
            Name = name, Purpose = purpose,
            TrueCommand = trueCmd, FalseCommand = falseCmd,
            CheckCommand = checkCmd,
        };
        target.UserTweaks.Add(newItem);
        await SaveConfigAsync(config);
        return newItem;
    }

    public async Task DeleteUserTweakAsync(AppConfig config, string name)
    {
        var target = FindUserTweakFeature(config);
        var item = target?.UserTweaks.FirstOrDefault(i => i.Name == name);
        if (item == null) return;
        target!.UserTweaks.Remove(item);
        await SaveConfigAsync(config);
    }
}

public class UserPrefs
{
    public ThemeConfig? Theme { get; set; }
    public List<Item> UserTweaks { get; set; } = new();
}
