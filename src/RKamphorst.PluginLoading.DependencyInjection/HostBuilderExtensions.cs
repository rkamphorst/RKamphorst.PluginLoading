using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginConfiguration;
using RKamphorst.PluginConfiguration.Contract;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.DependencyInjection;

public static class HostBuilderExtensions
{
    private const string PluginBuilderKey = nameof(PluginBuilder);

    public static async Task<IHostBuilder> ConfigurePluginsAsync(this IHostBuilder hostBuilder,
        Action<IPluginRegistration, ILoggerFactory> registerPlugins, bool addSupportForConfigurationPerPlugin = true)
    {
        using ILoggerFactory loggerFactory = CreateLoggerFactory(hostBuilder);

        var builder = new PluginBuilder(loggerFactory);
        hostBuilder.Properties[PluginBuilderKey] = builder;

        registerPlugins(builder, loggerFactory);
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

    private static ILoggerFactory CreateLoggerFactory(IHostBuilder hostBuilder)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return new DisposingLoggerFactory(loggerFactory, serviceProvider);
    }
    
    private class DisposingLoggerFactory : ILoggerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        private readonly ServiceProvider _serviceProvider;

        public DisposingLoggerFactory(ILoggerFactory loggerFactory, ServiceProvider serviceProvider)
        {
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            _loggerFactory.AddProvider(provider);
        }
    }
}