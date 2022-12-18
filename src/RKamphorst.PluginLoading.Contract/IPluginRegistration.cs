namespace RKamphorst.PluginLoading.Contract;

public interface IPluginRegistration
{
    IPluginRegistration AddPluginsFromSource(Type serviceType, IPluginLibrarySource fromSource);

    IPluginRegistration ShareWithPlugins(params Type[] sharedTypes);
}