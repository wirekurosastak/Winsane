using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Winsane.Core.Interfaces;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

/// <summary>
/// Service for managing application configuration.
/// Loads definitions from readonly embedded resources and applies persistent user preferences.
/// </summary>
public class ConfigService : IConfigService
{
    private static readonly string WinsaneFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane");
    private const string PrefsFileName = "user_prefs.json";
    
    private readonly IDeserializer _yamlDeserializer;

    public ConfigService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }
    
    private string PrefsFilePath => Path.Combine(WinsaneFolder, PrefsFileName);

    /// <summary>
    /// Load full application configuration.
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        // 1. Load Immutable Definitions
        var config = await LoadEmbeddedConfigAsync();
        
        // 2. Apply User Preferences (Mutable State)
        await ApplyUserPrefsAsync(config);
        
        return config;
    }

    /// <summary>
    /// Save current user preferences (Enabled items and Custom Tweaks).
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        EnsureWinsaneFolder();
        
        var prefs = new UserPrefs
        {
            Theme = config.Theme,
            EnabledItems = new Dictionary<string, bool>()
        };

        // Collect state
        foreach (var feature in config.Features)
        {
            if (feature.Items == null) continue;
            
            foreach (var item in feature.Items)
            {
                // Only save if explicitly Enabled and has a name
                if (item.Enabled && !string.IsNullOrEmpty(item.Name))
                {
                    prefs.EnabledItems[item.Name] = true;
                }
                
                // Collect sub-items
                if (item.SubItems != null)
                {
                    foreach (var sub in item.SubItems)
                    {
                        if (sub.Enabled && !string.IsNullOrEmpty(sub.Name)) prefs.EnabledItems[sub.Name] = true;
                    }
                }

                // Collect Custom Tweaks
                if (item.IsUserTweak)
                {
                    prefs.CustomTweaks.Add(item);
                }
            }
        }

        var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(PrefsFilePath, json);
    }
    
    private async Task ApplyUserPrefsAsync(AppConfig config)
    {
        if (!File.Exists(PrefsFilePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(PrefsFilePath);
            var prefs = JsonSerializer.Deserialize<UserPrefs>(json);
            
            if (prefs == null) return;
            
            // 1. Restore Theme
            if (prefs.Theme != null) config.Theme = prefs.Theme;
            
            // 2. Restore Enabled States
            if (prefs.EnabledItems != null)
            {
                foreach (var feature in config.Features)
                {
                    if (feature.Items == null) continue;
                    
                    foreach (var item in feature.Items)
                    {
                        if (!string.IsNullOrEmpty(item.Name) && prefs.EnabledItems.ContainsKey(item.Name))
                        {
                            item.Enabled = prefs.EnabledItems[item.Name];
                        }
                        
                        // SubItems
                        if (item.SubItems != null)
                        {
                            foreach (var sub in item.SubItems)
                            {
                                if (!string.IsNullOrEmpty(sub.Name) && prefs.EnabledItems.ContainsKey(sub.Name))
                                {
                                    sub.Enabled = prefs.EnabledItems[sub.Name];
                                }
                            }
                        }
                    }
                }
            }
            
            // 3. Restore Custom Tweaks
            if (prefs.CustomTweaks != null && prefs.CustomTweaks.Any())
            {
                var targetFeature = config.Features.FirstOrDefault(f => !string.IsNullOrEmpty(f.UserTweaksSection));
                if (targetFeature != null)
                {
                     // Ensure config-defined header exists
                    if (!targetFeature.Items.Any(i => i.Category == targetFeature.UserTweaksSection))
                    {
                        targetFeature.Items.Add(new Item { Category = targetFeature.UserTweaksSection });
                    }
                
                    foreach (var tweak in prefs.CustomTweaks)
                    {
                        // Avoid duplicates
                        if (!targetFeature.Items.Any(i => i.Name == tweak.Name))
                        {
                            tweak.IsUserTweak = true; // Ensure flag is set
                            targetFeature.Items.Add(tweak);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading prefs: {ex.Message}");
        }
    }
    
    // --- Helper Methods ---

    private async Task<AppConfig> LoadEmbeddedConfigAsync()
    {
        var assembly = typeof(ConfigService).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();
        var actualResource = resourceNames.FirstOrDefault(r => r.EndsWith("data.yaml"));
        
        if (actualResource != null)
        {
            using var stream = assembly.GetManifestResourceStream(actualResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var yaml = await reader.ReadToEndAsync();
                return ParseConfig(yaml);
            }
        }
        
        // Fallback for dev
        var devPath = Path.Combine(AppContext.BaseDirectory, "Assets", "data.yaml");
        if (File.Exists(devPath))
        {
            var yaml = await File.ReadAllTextAsync(devPath);
            return ParseConfig(yaml);
        }
        
        return new AppConfig();
    }

    private AppConfig ParseConfig(string yaml)
    {
        return _yamlDeserializer.Deserialize<AppConfig>(yaml);
    }
    
    private void EnsureWinsaneFolder()
    {
        if (!Directory.Exists(WinsaneFolder))
        {
            Directory.CreateDirectory(WinsaneFolder);
        }
    }
    
    public async Task<Item?> AddUserTweakAsync(AppConfig config, string name, string purpose, string trueCmd, string falseCmd)
    {
        var targetFeature = config.Features.FirstOrDefault(f => !string.IsNullOrEmpty(f.UserTweaksSection));
        if (targetFeature == null) return null;

        var newItem = new Item
        {
            Name = name,
            Purpose = purpose,
            TrueCommand = trueCmd,
            FalseCommand = falseCmd,
            Enabled = false,
            IsUserTweak = true
        };
        
        targetFeature.Items.Add(newItem);
        await SaveConfigAsync(config); // Auto-save on add
        return newItem;
    }
    
    public async Task DeleteUserTweakAsync(AppConfig config, string name)
    {
        var targetFeature = config.Features.FirstOrDefault(f => !string.IsNullOrEmpty(f.UserTweaksSection));
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
    public Dictionary<string, bool> EnabledItems { get; set; } = new();
    public List<Item> CustomTweaks { get; set; } = new();
}
