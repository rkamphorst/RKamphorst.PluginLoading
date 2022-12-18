using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public interface IPluginLibrary
{
    PluginLibraryReference Reference { get; }

    IReadOnlyCollection<Type> ServiceTypes { get; }
    
    IReadOnlyCollection<Type> SharedTypes { get; }
    
    Task<IEnumerable<Plugin>> GetPluginsAsync(CancellationToken cancellationToken);
}