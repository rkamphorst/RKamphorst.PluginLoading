namespace RKamphorst.PluginLoading.Aws.Options;

public class EcsServiceUpdaterOptions
{
    public ServiceAndCluster[]? Services { get; set; }

    public int DelayMillis { get; set; } = 15000;
}