namespace RKamphorst.PluginLoading.Aws;

public interface IEcsServiceUpdater
{
    Task UpdateAsync(CancellationToken cancellationToken);
}