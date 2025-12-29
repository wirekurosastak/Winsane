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
    /// Initialize configuration - load from embedded resource or local file
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        // First, try to load from local Winsane folder
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
        
        // Load from embedded resource
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
                new Feature { Name = "Optimizer", Categories = new List<Category>() },
                new Feature { Name = "Cleaner", Categories = new List<Category>() },
                new Feature { Name = "Apps", Categories = new List<Category>() },
                new Feature { Name = "Admin Tools", Categories = new List<Category>() },
                new Feature { Name = "Dashboard", Categories = new List<Category>() }
            }
        };
    }
    
    /// <summary>
    /// Add a user-created tweak to the Optimizer/User category
    /// </summary>
    public async Task<Item?> AddUserTweakAsync(AppConfig config, string name, string purpose, string trueCmd, string falseCmd)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(trueCmd) || string.IsNullOrWhiteSpace(falseCmd))
        {
            return null;
        }
        
        // Find or create Optimizer/User category
        var optimizer = config.Features.FirstOrDefault(f => f.Name == "Optimizer");
        if (optimizer == null)
        {
            optimizer = new Feature { Name = "Optimizer" };
            config.Features.Insert(0, optimizer);
        }
        
        var userCategory = optimizer.Categories.FirstOrDefault(c => c.Name == "User");
        if (userCategory == null)
        {
            userCategory = new Category { Name = "User" };
            optimizer.Categories.Add(userCategory);
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
        
        userCategory.Items.Add(newItem);
        
        // Save config
        await SaveConfigAsync(config);
        
        return newItem;
    }
    
    /// <summary>
    /// Delete a user-created tweak from the Optimizer/User category
    /// </summary>
    public async Task<bool> DeleteUserTweakAsync(AppConfig config, string tweakName)
    {
        var optimizer = config.Features.FirstOrDefault(f => f.Name == "Optimizer");
        var userCategory = optimizer?.Categories.FirstOrDefault(c => c.Name == "User");
        
        if (userCategory == null) return false;
        
        var item = userCategory.Items.FirstOrDefault(i => i.Name == tweakName);
        if (item == null) return false;
        
        userCategory.Items.Remove(item);
        await SaveConfigAsync(config);
        
        return true;
    }
}
