using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public class PluginBuilder : IPluginBuilder, IPluginRegistration
{
    private readonly IPluginAssemblyLoaderFactory _assemblyLoaderFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly Dictionary<IPluginLibrarySource, PluginLibraryFactory> _pluginLibraryFactories;
    private readonly List<Type> _sharedTypes;
    
    public PluginBuilder(ILoggerFactory loggerFactory)
        : this( new PluginAssemblyLoaderFactory(loggerFactory), loggerFactory) { }
    
    public PluginBuilder(IPluginAssemblyLoaderFactory assemblyLoaderFactory, ILoggerFactory loggerFactory)
    {
        _assemblyLoaderFactory = assemblyLoaderFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PluginBuilder>();
        _pluginLibraryFactories = new Dictionary<IPluginLibrarySource, PluginLibraryFactory>();
        _sharedTypes = new List<Type>();
    }

    public IPluginRegistration AddPluginsFromSource(Type serviceType, IPluginLibrarySource fromSource)
    {
        if (!_pluginLibraryFactories.TryGetValue(fromSource, out PluginLibraryFactory? factory))
        {
            factory = new PluginLibraryFactory(_assemblyLoaderFactory, fromSource, _loggerFactory);
            factory.AddSharedTypes(_sharedTypes.ToArray());
            _pluginLibraryFactories.Add(fromSource, factory);
        }
        factory.AddServiceTypes(serviceType);
        return this;
    }
    
    public IPluginRegistration ShareWithPlugins(params Type[] sharedTypes)
    {
        _sharedTypes.AddRange(sharedTypes);
        foreach (PluginLibraryFactory factory in _pluginLibraryFactories.Values)
        {
            factory.AddSharedTypes(sharedTypes);
        }
        return this;
    }
    
    public async Task<IEnumerable<Plugin>> BuildAsync(CancellationToken cancellationToken)
    {
        List<Plugin> result = new List<Plugin>();
        foreach (var factory in _pluginLibraryFactories.Values)
        {
            var libraries = await factory.GetLibrariesAsync(cancellationToken);
            foreach (var lib in libraries)
            {
                var plugins = (await lib.GetPluginsAsync(cancellationToken)).ToArray();
                _logger.LogInformation(
                    "Found plugin types {@PluginTypes} in library {PluginLibrary}",
                    plugins.Select(p => p.Implementation.Name), lib.Reference
                );
                result.AddRange(plugins);
            }
        }
        return result;
    }
    
}