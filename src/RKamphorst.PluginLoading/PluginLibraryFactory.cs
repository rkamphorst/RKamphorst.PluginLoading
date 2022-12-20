using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

internal class PluginLibraryFactory
{
    private readonly IPluginAssemblyLoaderFactory _assemblyLoaderFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginLibraryFactory> _logger;
    public List<Type> SharedTypes { get; }
    public List<Type> ServiceTypes { get; }
    
    public IPluginLibrarySource LibrarySource { get; }

    private Task<IPluginLibrary[]>? _createLibrariesTask;
    

    public PluginLibraryFactory(IPluginAssemblyLoaderFactory assemblyLoaderFactory, IPluginLibrarySource librarySource, ILoggerFactory loggerFactory)
    {
        _assemblyLoaderFactory = assemblyLoaderFactory;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PluginLibraryFactory>();
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

    public async Task<IPluginLibrary[]> GetLibrariesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await (_createLibrariesTask ??= CreateLibrariesAsync());
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation canceled, resetting create libraries task");
            _createLibrariesTask = null;
            throw;
        }

        async Task<IPluginLibrary[]> CreateLibrariesAsync()
        {
            var libRefs = (await LibrarySource.GetListAsync(cancellationToken)).ToArray();

            _logger.LogInformation(
                "Library source {PluginLibrarySource} has {PluginLibrariesCount} libraries: {@PluginLibraries}",
                LibrarySource.Name, libRefs.Length, libRefs.Select(r => r.Name) 
            );
            
            return libRefs
                .Select(libRef => (IPluginLibrary)new PluginLibrary(
                    _assemblyLoaderFactory, libRef, ServiceTypes, SharedTypes, _loggerFactory
                ))
                .ToArray();
        }
    }


}