using System.Net.Http;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using WinsaneCS.Models;

namespace WinsaneCS.Services;

/// <summary>
/// Service for loading and saving configuration
/// </summary>
public class ConfigService
{
    private static readonly string WinsaneFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winsane");
    private const string DataFileName = "data.yaml";
    private const string GitHubRawUrl = "https://raw.githubusercontent.com/wirekurosastak/Winsane/refs/heads/WinsaneC%23/Assets/data.yaml";
    
    
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    
    public ConfigService()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
            
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }
    
    public string DataFilePath => Path.Combine(WinsaneFolder, DataFileName);
    
    /// <summary>
    /// Initialize configuration - load from local file, AppData, or embedded resource
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        // 1. First, try to load from local Assets folder (for development)
        var devPath = Path.Combine(AppContext.BaseDirectory, "Assets", "data.yaml");
        if (File.Exists(devPath))
        {
            try
            {
                var devConfig = await LoadFromFileAsync(devPath);
                if (devConfig != null)
                {
                    return devConfig;
                }
            }
            catch { /* Fall through */ }
        }
        
        // 2. Try to load from local Winsane folder (user's saved config)
        if (File.Exists(DataFilePath))
        {
            try
            {
                var localConfig = await LoadFromFileAsync(DataFilePath);
                if (localConfig != null)
                {
                    return localConfig;
                }
            }
            catch { /* Fall through to embedded */ }
        }
        
        // 3. Load from embedded resource
        var embeddedConfig = await LoadEmbeddedConfigAsync();
        
        // Ensure Winsane folder exists and save config there
        EnsureWinsaneFolder();
        await SaveConfigAsync(embeddedConfig);
        
        return embeddedConfig;
    }
    
    /// <summary>
    /// Load configuration from embedded resource
    /// </summary>
    private async Task<AppConfig> LoadEmbeddedConfigAsync()
    {
        var assembly = typeof(ConfigService).Assembly;
        
        // Try to find the resource
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
        
        // Fall back to file in Assets folder (for development)
        var devPath = Path.Combine(AppContext.BaseDirectory, "Assets", "data.yaml");
        if (File.Exists(devPath))
        {
            return await LoadFromFileAsync(devPath) ?? CreateDefaultConfig();
        }
        
        return CreateDefaultConfig();
    }
    
    /// <summary>
    /// Load configuration from file
    /// </summary>
    private async Task<AppConfig?> LoadFromFileAsync(string path)
    {
        try
        {
            var yaml = await File.ReadAllTextAsync(path);
            return ParseConfig(yaml);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Parse YAML to AppConfig
    /// </summary>
    private AppConfig ParseConfig(string yaml)
    {
        return _deserializer.Deserialize<AppConfig>(yaml);
    }
    
    /// <summary>
    /// Save configuration to file
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        EnsureWinsaneFolder();
        
        // For now, we'll save a simplified version tracking enabled states
        var yaml = _serializer.Serialize(config);
        await File.WriteAllTextAsync(DataFilePath, yaml);
    }
    
    /// <summary>
    /// Ensure the Winsane folder exists
    /// </summary>
    private void EnsureWinsaneFolder()
    {
        if (!Directory.Exists(WinsaneFolder))
        {
            Directory.CreateDirectory(WinsaneFolder);
        }
    }
    
    /// <summary>
    /// Create a minimal default configuration
    /// </summary>
    private AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            Theme = new ThemeConfig(),
            Features = new List<Feature>
            {
                new Feature { Name = "Optimizer" },
                new Feature { Name = "Cleaner" },
                new Feature { Name = "Apps" },
                new Feature { Name = "Admin Tools" },
                new Feature { Name = "Dashboard" }
            }
        };
    }
    
    /// <summary>
    /// Add a user-created tweak to the Optimizer feature
    /// </summary>
    public async Task<Item?> AddUserTweakAsync(AppConfig config, string name, string purpose, string trueCmd, string falseCmd)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(trueCmd) || string.IsNullOrWhiteSpace(falseCmd))
        {
            return null;
        }
        
        // Find or create Optimizer feature
        var optimizer = config.Features.FirstOrDefault(f => f.Name == "Optimizer");
        if (optimizer == null)
        {
            optimizer = new Feature { Name = "Optimizer" };
            config.Features.Insert(0, optimizer);
        }
        
        // Ensure "User" header exists
        var userHeader = optimizer.Items.FirstOrDefault(i => i.IsHeader && i.Header == "User");
        if (userHeader == null)
        {
            optimizer.Items.Add(new Item { Header = "User" });
        }

        // Create the new tweak
        var newItem = new Item
        {
            Name = name,
            Purpose = string.IsNullOrWhiteSpace(purpose) ? "User-defined tweak." : purpose,
            TrueCommand = trueCmd,
            FalseCommand = falseCmd,
            Enabled = false,
            IsUserTweak = true
        };
        
        optimizer.Items.Add(newItem);
        
        // Save config
        await SaveConfigAsync(config);
        
        return newItem;
    }
    
    /// <summary>
    /// Delete a user-created tweak from the Optimizer feature
    /// </summary>
    public async Task<bool> DeleteUserTweakAsync(AppConfig config, string tweakName)
    {
        var optimizer = config.Features.FirstOrDefault(f => f.Name == "Optimizer");
        if (optimizer == null) return false;
        
        var item = optimizer.Items.FirstOrDefault(i => i.Name == tweakName && i.IsUserTweak);
        if (item == null) return false;
        
        optimizer.Items.Remove(item);
        
        // Check if we need to remove the "User" header if it's empty
        var userHeaderIndex = optimizer.Items.FindIndex(i => i.IsHeader && i.Header == "User");
        if (userHeaderIndex != -1)
        {
            // If the header is the last item, or the next item is another header, it's empty
            bool isNextHeaderOrEnd = userHeaderIndex == optimizer.Items.Count - 1 || 
                                     (optimizer.Items[userHeaderIndex + 1].IsHeader);
                                     
            // However, we need to be careful. IsUserTweak is a runtime flag, it might not be set after reload unless we logic it.
            // But here we are manipulating the in-memory config object which should have valid flags if loaded/added correctly.
            // For safety, let's just leave the header. It's harmless.
        }

        await SaveConfigAsync(config);
        
        return true;
    }
}
