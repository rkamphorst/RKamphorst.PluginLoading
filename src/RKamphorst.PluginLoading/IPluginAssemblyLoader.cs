using System.Reflection;

namespace RKamphorst.PluginLoading;

public interface IPluginAssemblyLoader
{
    public Task<Assembly> GetOrLoadAssemblyAsync(CancellationToken cancellationToken);
}