using System.Reflection;
using McMaster.NETCore.Plugins;

namespace RKamphorst.PluginLoading;

public class PluginAssemblyLoaderFactory : IPluginAssemblyLoaderFactory
{
    private readonly IPluginStore _store;

    public PluginAssemblyLoaderFactory() : this(new PluginStore())
    {
    }


    public PluginAssemblyLoaderFactory(IPluginStore store)
    {
        _store = store;
    }
    
    public IPluginAssemblyLoader Create(IPluginLibrary forLibrary)
    {
        return new PluginAssemblyLoader(forLibrary, _store);
    }

    private class PluginAssemblyLoader : IPluginAssemblyLoader
    {
        private readonly IPluginLibrary _forLibrary;
        private readonly IPluginStore _store;
        private Task<Assembly>? _assemblyLoadTask;

        private PluginLoader? _pluginLoader;
        
        public PluginAssemblyLoader(IPluginLibrary forLibrary, IPluginStore store)
        {
            _forLibrary = forLibrary;
            _store = store;
        }

        public async Task<Assembly> GetOrLoadAssemblyAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await (_assemblyLoadTask ??= LoadAssemblyAsync());
            }
            catch (OperationCanceledException)
            {
                _assemblyLoadTask = null;
                throw;
            }

            async Task<Assembly> LoadAssemblyAsync()
            {
                var libraryAssemblyFile =
                    await _store.GetPathToLibraryAssemblyAsync(_forLibrary.Reference, cancellationToken);
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