using System.Text.Json;
using Winsane.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Winsane.Infrastructure.Services;

public class ConfigService
{
    private static readonly string WinsaneFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Winsane"
    );
    private const string PrefsFileName = "user_tweaks.json";
    private const string RemoteConfigUrl =
        "https://raw.githubusercontent.com/wirekurosastak/Winsane/refs/heads/WinsaneC%23/Assets/data.yaml";

    private readonly IDeserializer _yamlDeserializer;
    private readonly HttpClient _httpClient;

    public ConfigService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

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
        if (!Directory.Exists(WinsaneFolder))
            Directory.CreateDirectory(WinsaneFolder);

        var prefs = new UserPrefs
        {
            Theme = config.Theme,

            UserTweaks = FlattenItems(config.Features).Where(i => i.IsUserTweak).ToList(),
        };

        var json = JsonSerializer.Serialize(
            prefs,
            new JsonSerializerOptions 
            { 
                WriteIndented = true, 
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault 
            }
        );
        await File.WriteAllTextAsync(PrefsFilePath, json);
    }

    private async Task<AppConfig> FetchBaseConfigAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(RemoteConfigUrl);
            if (response.IsSuccessStatusCode)
            {
                return _yamlDeserializer.Deserialize<AppConfig>(
                    await response.Content.ReadAsStringAsync()
                );
            }
        }
        catch { }

        var resourceName = "Winsane.Assets.data.yaml";
        using var stream =
            typeof(ConfigService).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Critical: Embedded config not found.");

        using var reader = new StreamReader(stream);
        return _yamlDeserializer.Deserialize<AppConfig>(await reader.ReadToEndAsync()) ?? new AppConfig();
    }

    private async Task ApplyUserPrefsAsync(AppConfig config)
    {
        if (!File.Exists(PrefsFilePath))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(PrefsFilePath);
            var prefs = JsonSerializer.Deserialize<UserPrefs>(json);
            if (prefs == null)
                return;

            if (prefs.Theme != null)
                config.Theme = prefs.Theme;

            if (prefs.UserTweaks?.Any() == true)
            {
                var targetFeature = config.Features.FirstOrDefault(f =>
                    f.UserTweaksSection != null
                );
                if (targetFeature != null)
                {
                    foreach (var tweak in prefs.UserTweaks)
                    {
                        if (!targetFeature.UserTweaks.Any(i => i.Name == tweak.Name))
                        {
                            tweak.IsUserTweak = true;
                            targetFeature.UserTweaks.Add(tweak);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private IEnumerable<Item> FlattenItems(List<Feature> features)
    {
        if (features == null)
            yield break;

        foreach (var f in features)
        {
            if (f.Items != null)
            {
                foreach (var i in f.Items)
                {
                    yield return i;
                    if (i.SubItems != null)
                    {
                        foreach (var s in i.SubItems)
                            yield return s;
                    }
                }
            }
            
            if (f.UserTweaks != null)
            {
                foreach (var i in f.UserTweaks)
                {
                    yield return i;
                }
            }
        }
    }

    public async Task<Item?> AddUserTweakAsync(
        AppConfig config,
        string name,
        string purpose,
        string trueCmd,
        string falseCmd,
        string checkCmd
    )
    {
        var targetFeature = config.Features.FirstOrDefault(f => f.UserTweaksSection != null);
        if (targetFeature == null)
            return null;

        var newItem = new Item
        {
            Name = name,
            Purpose = purpose,
            TrueCommand = trueCmd,
            FalseCommand = falseCmd,
            CheckCommand = checkCmd,
            IsUserTweak = true,
        };

        targetFeature.UserTweaks.Add(newItem);
        await SaveConfigAsync(config);
        return newItem;
    }

    public async Task DeleteUserTweakAsync(AppConfig config, string name)
    {
        var targetFeature = config.Features.FirstOrDefault(f => f.UserTweaksSection != null);
        if (targetFeature == null)
            return;

        var item = targetFeature.UserTweaks.FirstOrDefault(i => i.Name == name && i.IsUserTweak);
        if (item != null)
        {
            targetFeature.UserTweaks.Remove(item);
            await SaveConfigAsync(config);
        }
    }
}

public class UserPrefs
{
    public ThemeConfig? Theme { get; set; }
    public List<Item> UserTweaks { get; set; } = new();
}
