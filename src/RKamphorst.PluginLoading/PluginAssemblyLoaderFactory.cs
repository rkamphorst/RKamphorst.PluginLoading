using System.Reflection;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;

namespace RKamphorst.PluginLoading;

public class PluginAssemblyLoaderFactory : IPluginAssemblyLoaderFactory
{
    private readonly IPluginStore _store;
    private readonly ILoggerFactory _loggerFactory;

    public PluginAssemblyLoaderFactory(ILoggerFactory loggerFactory) 
        : this(new PluginStore(loggerFactory.CreateLogger<PluginStore>()), loggerFactory)
    {
    }


    public PluginAssemblyLoaderFactory(IPluginStore store, ILoggerFactory loggerFactory)
    {
        _store = store;
        _loggerFactory = loggerFactory;
    }
    
    public IPluginAssemblyLoader Create(IPluginLibrary forLibrary)
    {
        return new PluginAssemblyLoader(forLibrary, _store, _loggerFactory.CreateLogger<PluginAssemblyLoader>());
    }

    private class PluginAssemblyLoader : IPluginAssemblyLoader
    {
        private readonly IPluginLibrary _forLibrary;
        private readonly IPluginStore _store;
        private readonly ILogger<PluginAssemblyLoader> _logger;
        private Task<Assembly>? _assemblyLoadTask;

        private PluginLoader? _pluginLoader;
        
        public PluginAssemblyLoader(IPluginLibrary forLibrary, IPluginStore store, ILogger<PluginAssemblyLoader> logger)
        {
            _forLibrary = forLibrary;
            _store = store;
            _logger = logger;
        }

        public async Task<Assembly> GetOrLoadAssemblyAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await (_assemblyLoadTask ??= LoadAssemblyAsync());
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Operation canceled, resetting assembly load task");
                _assemblyLoadTask = null;
                throw;
            }

            async Task<Assembly> LoadAssemblyAsync()
            {
                var libraryAssemblyFile =
                    await _store.GetPathToLibraryAssemblyAsync(_forLibrary.Reference, cancellationToken);
                
                _logger.LogDebug(
                    "Loading library {LibraryReference} from {LibraryAssemblyFile} " +
                    "(services {@ServiceTypes}, shared {@SharedTypes})",
                    _forLibrary.Reference, libraryAssemblyFile, 
                    _forLibrary.ServiceTypes.Select(t => t.Name), 
                    _forLibrary.SharedTypes.Select(t => t.Name));
                
                _pluginLoader =
                    PluginLoader.CreateFromAssemblyFile(
                        libraryAssemblyFile, false,
                        _forLibrary.ServiceTypes.Concat(_forLibrary.SharedTypes).ToArray()
                    );
                return _pluginLoader.LoadDefaultAssembly();
            }
        }
    }
}