using Amazon.ECS;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.Aws.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginLoadingFromAws(this IServiceCollection services, Action<PluginLoadingAwsOptions> configure)
    {
        services
            .TryAddAWSService<IAmazonECS>()
            .TryAddAWSService<IAmazonS3>()
            .AddLogging()

            .Configure(configure)

            .AddScoped(sp => sp.GetRequiredService<IOptions<PluginLoadingAwsOptions>>().Value.S3LibrarySource)
            .AddScoped<S3LibrarySource>()
            .AddScoped<IPluginLibraryTimestampProvider>(sp => sp.GetRequiredService<S3LibrarySource>())
            .AddScoped<IPluginLibrarySource>(sp => sp.GetRequiredService<S3LibrarySource>())

            .AddScoped(sp => sp.GetRequiredService<IOptions<PluginLoadingAwsOptions>>().Value.EcsServiceUpdater)
            .AddScoped<IEcsServiceUpdater, EcsServiceUpdater>();

        return services;
    }
}