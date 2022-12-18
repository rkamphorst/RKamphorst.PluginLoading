using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RKamphorst.PluginConfiguration;
using RKamphorst.PluginConfiguration.Contract;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.DependencyInjection;

public static class HostBuilderExtensions
{
    private const string PluginBuilderKey = nameof(PluginBuilder);

    public static async Task<IHostBuilder> ConfigurePluginsAsync(this IHostBuilder hostBuilder,
        Action<IPluginRegistration> registerPlugins, bool addSupportForConfigurationPerPlugin = true)
    {
        var builder = new PluginBuilder();
        hostBuilder.Properties[PluginBuilderKey] = builder;

        registerPlugins(builder);
        if (addSupportForConfigurationPerPlugin)
        {
            builder.ShareWithPlugins(typeof(IPluginOptions<>));
        }

        var plugins = await builder.BuildAsync(CancellationToken.None);

        ConfigurePluginServices(hostBuilder, plugins);

        if (addSupportForConfigurationPerPlugin)
        {
            hostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(typeof(IPluginOptions<>), typeof(PluginOptions<>));
            });
        }
        
        return hostBuilder;
    }

    private static void ConfigurePluginServices(IHostBuilder hostBuilder, IEnumerable<Plugin> plugins)
    {
        hostBuilder.ConfigureServices(services =>
        {
            foreach (var plugin in plugins)
            {
                var pluginImpl = plugin.Implementation;
                var pluginServices = plugin.Services;
                if (pluginServices.Length == 1)
                {
                    services.AddTransient(pluginServices[0], pluginImpl);
                }
                else
                {
                    services.AddTransient(pluginImpl, pluginImpl);
                    foreach (var pluginService in pluginServices)
                    {
                        services.AddTransient(
                            pluginService,
                            sp => sp.GetRequiredService(pluginImpl)
                        );
                    }
                }
            }
        });
    }
}