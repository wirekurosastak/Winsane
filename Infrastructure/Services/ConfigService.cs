using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

/// <summary>
/// Simplified ConfigService.
/// Sources: GitHub (Primary) -> Embedded (Fallback).
/// Persistence: user_prefs.json (Theme, Enabled States, Custom Tweaks).
/// </summary>
public class ConfigService
{
    private static readonly string WinsaneFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane");
    private const string PrefsFileName = "user_prefs.json";
    private const string RemoteConfigUrl = "https://raw.githubusercontent.com/wirekurosastak/Winsane/refs/heads/WinsaneC%23/Assets/data.yaml";

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
        // 1. Fetch Base Config (GitHub -> Embedded)
        var config = await FetchBaseConfigAsync();

        // 2. Apply Mutable User Preferences
        await ApplyUserPrefsAsync(config);

        return config;
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        if (!Directory.Exists(WinsaneFolder)) Directory.CreateDirectory(WinsaneFolder);

        var prefs = new UserPrefs
        {
            Theme = config.Theme,
            // Collect User Tweaks
            CustomTweaks = FlattenItems(config.Features)
                .Where(i => i.IsUserTweak)
                .ToList()
        };

        var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(PrefsFilePath, json);
    }

    private async Task<AppConfig> FetchBaseConfigAsync()
    {
        // Try GitHub first
        try
        {
            var response = await _httpClient.GetAsync(RemoteConfigUrl);
            if (response.IsSuccessStatusCode)
            {
                return _yamlDeserializer.Deserialize<AppConfig>(await response.Content.ReadAsStringAsync());
            }
        }
        catch 
        { 
            // Silent fallback to embedded - network failures expected
        }

        // Fallback: Embedded Resource (Winsane.Assets.data.yaml)
        var resourceName = "Winsane.Assets.data.yaml";
        using var stream = typeof(ConfigService).Assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException("Critical: Embedded config not found.");

        using var reader = new StreamReader(stream);
        return _yamlDeserializer.Deserialize<AppConfig>(await reader.ReadToEndAsync());
    }

    private async Task ApplyUserPrefsAsync(AppConfig config)
    {
        if (!File.Exists(PrefsFilePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(PrefsFilePath);
            var prefs = JsonSerializer.Deserialize<UserPrefs>(json);
            if (prefs == null) return;

            // 1. Theme
            if (prefs.Theme != null) config.Theme = prefs.Theme;


            // 2. Custom Tweaks
            if (prefs.CustomTweaks?.Any() == true)
            {
                var targetFeature = config.Features.FirstOrDefault(f => f.UserTweaksSection != null);
                if (targetFeature != null)
                {
                    // Ensure header exists
                    if (!targetFeature.Items.Any(i => i.Category == targetFeature.UserTweaksSection))
                        targetFeature.Items.Add(new Item { Category = targetFeature.UserTweaksSection });

                    foreach (var tweak in prefs.CustomTweaks)
                    {
                        if (!targetFeature.Items.Any(i => i.Name == tweak.Name))
                        {
                            tweak.IsUserTweak = true;
                            targetFeature.Items.Add(tweak);
                        }
                    }
                }
            }
        }
        catch 
        { 
            // Ignore corrupt prefs - start fresh
        }
    }

    // Helper to flatten hierarchy (Features -> Items -> SubItems)
    private IEnumerable<Item> FlattenItems(List<Feature> features)
    {
        if (features == null) yield break;
        
        foreach (var f in features)
        {
            if (f.Items == null) continue;
            foreach (var i in f.Items)
            {
                yield return i;
                if (i.SubItems != null)
                {
                    foreach (var s in i.SubItems) yield return s;
                }
            }
        }
    }

    public async Task<Item?> AddUserTweakAsync(AppConfig config, string name, string purpose, string trueCmd, string falseCmd)
    {
        var targetFeature = config.Features.FirstOrDefault(f => f.UserTweaksSection != null);
        if (targetFeature == null) return null;

        var newItem = new Item
        {
            Name = name, Purpose = purpose, TrueCommand = trueCmd, FalseCommand = falseCmd,
            IsUserTweak = true
        };
        
        targetFeature.Items.Add(newItem);
        await SaveConfigAsync(config);
        return newItem;
    }
    
    public async Task DeleteUserTweakAsync(AppConfig config, string name)
    {
        var targetFeature = config.Features.FirstOrDefault(f => f.UserTweaksSection != null);
        if (targetFeature == null) return;
        
        var item = targetFeature.Items.FirstOrDefault(i => i.Name == name && i.IsUserTweak);
        if (item != null)
        {
            targetFeature.Items.Remove(item);
            await SaveConfigAsync(config);
        }
    }
}

public class UserPrefs
{
    public ThemeConfig? Theme { get; set; }
    public List<Item> CustomTweaks { get; set; } = new();
}
