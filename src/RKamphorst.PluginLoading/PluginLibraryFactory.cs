using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

internal class PluginLibraryFactory
{
    private readonly IPluginAssemblyLoaderFactory _assemblyLoaderFactory;
    public List<Type> SharedTypes { get; }
    public List<Type> ServiceTypes { get; }
    
    public IPluginLibrarySource LibrarySource { get; }

    private Task<IPluginLibrary[]>? _createLibrariesTask;

    public PluginLibraryFactory(IPluginAssemblyLoaderFactory assemblyLoaderFactory, IPluginLibrarySource librarySource)
    {
        _assemblyLoaderFactory = assemblyLoaderFactory;
        SharedTypes = new List<Type>();
        ServiceTypes = new List<Type>();
        LibrarySource = librarySource;
    }

    public void AddSharedTypes(params Type[] types)
    {
        AssertLibrariesAreNotCreated();
        SharedTypes.AddRange(types);
    }
    
    public void AddServiceTypes(params Type[] types)
    {
        AssertLibrariesAreNotCreated();
        ServiceTypes.AddRange(types);
    }

    private void AssertLibrariesAreNotCreated()
    {
        if (_createLibrariesTask != null)
        {
            throw new InvalidOperationException($"{nameof(GetLibrariesAsync)} was already called");
        }
    }
    
    public Task<IPluginLibrary[]> GetLibrariesAsync(CancellationToken cancellationToken)
    {
        return _createLibrariesTask ??= CreateLibrariesAsync();

        async Task<IPluginLibrary[]> CreateLibrariesAsync()
        {
            try
            {
                var libRefs = await LibrarySource.GetListAsync(cancellationToken);

                return libRefs
                    .Select(libRef => (IPluginLibrary)new PluginLibrary(
                        _assemblyLoaderFactory, libRef, ServiceTypes, SharedTypes
                    ))
                    .ToArray();
            }
            catch (OperationCanceledException)
            {
                // Task.Yield is needed here because otherwise,
                // _createLibrariesTask may be set to the returned 
                // Task *after* it is set to null below.
                // This can happen if LibrarySource.GetListAsync
                // is secretly not really async (as it is in unit tests)
                await Task.Yield();
                _createLibrariesTask = null;
                throw;
            }
        }
    }
    
    
}