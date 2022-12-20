using RKamphorst.PluginLoading.Aws.Options;

namespace RKamphorst.PluginLoading.Aws.DependencyInjection;

public class PluginLoadingAwsOptions
{
    public EcsServiceUpdaterOptions EcsServiceUpdater { get; set; } = new();
    public S3LibrarySourceOptions S3LibrarySource { get; set; } = new();
}