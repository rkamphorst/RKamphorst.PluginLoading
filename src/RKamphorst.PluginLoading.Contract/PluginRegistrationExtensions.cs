namespace RKamphorst.PluginLoading.Contract;

public static class PluginRegistrationExtensions
{
    public static IPluginRegistration AddPluginsFromSource<TService>(this IPluginRegistration registration,
        IPluginLibrarySource fromSource) =>
        registration.AddPluginsFromSource(typeof(TService), fromSource);

    public static IPluginRegistration  ShareWithPlugins<TShared>(this IPluginRegistration registration) =>
        registration.ShareWithPlugins(typeof(TShared));
}