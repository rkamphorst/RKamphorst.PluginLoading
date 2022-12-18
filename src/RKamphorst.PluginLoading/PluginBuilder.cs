using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public class PluginBuilder : IPluginBuilder, IPluginRegistration
{
    private readonly IPluginAssemblyLoaderFactory _assemblyLoaderFactory;
    private readonly Dictionary<IPluginLibrarySource, PluginLibraryFactory> _pluginLibraryFactories;
    private readonly List<Type> _sharedTypes;

    public PluginBuilder()
        : this(new PluginAssemblyLoaderFactory())
    {
    }

    public PluginBuilder(IPluginAssemblyLoaderFactory assemblyLoaderFactory)
    {
        _assemblyLoaderFactory = assemblyLoaderFactory;
        _pluginLibraryFactories = new Dictionary<IPluginLibrarySource, PluginLibraryFactory>();
        _sharedTypes = new List<Type>();
    }

    public IPluginRegistration AddPluginsFromSource(Type serviceType, IPluginLibrarySource fromSource)
    {
        if (!_pluginLibraryFactories.TryGetValue(fromSource, out PluginLibraryFactory? factory))
        {
            factory = new PluginLibraryFactory(_assemblyLoaderFactory, fromSource);
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
                var plugins = await lib.GetPluginsAsync(cancellationToken);
                result.AddRange(plugins);
            }
        }
        return result;
    }
    
}