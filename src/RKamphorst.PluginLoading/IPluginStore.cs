using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public interface IPluginStore
{
    Task<string> GetPathToLibraryAssemblyAsync(PluginLibraryReference lib, CancellationToken cancellationToken);
}