namespace RKamphorst.PluginLoading;

public interface IPluginAssemblyLoaderFactory
{
    IPluginAssemblyLoader Create(IPluginLibrary forLibrary);
}