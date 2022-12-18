using System.Text.Json;
using System.Text.Json.Serialization;
using RKamphorst.PluginConfiguration.Contract;

namespace RKamphorst.PluginConfiguration;

public class PluginOptions<TPluginOptions> : IPluginOptions<TPluginOptions>
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        DictionaryKeyPolicy = null,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
        WriteIndented = true
    };
    
    private TPluginOptions? _pluginOptions;
    private bool _optionsAreCreated;
    
    public TPluginOptions? Value => GetOrCreateOptions();

    public TPluginOptions? GetOrCreateOptions()
    {
        if (_optionsAreCreated)
        {
            return _pluginOptions;
        }

        _optionsAreCreated = true;

        var assemblyDirectory = Path.GetDirectoryName(typeof(TPluginOptions).Assembly.Location);
        if (assemblyDirectory != null)
        {
            var configFileName = Path.Combine(assemblyDirectory, Convention.ConfigurationFileName);
            if (File.Exists(configFileName))
            {
                using var stream = File.OpenRead(configFileName);
                return _pluginOptions = JsonSerializer.Deserialize<TPluginOptions>(stream, _serializerOptions);
            }
        }
        
        return _pluginOptions = default;
    }
}