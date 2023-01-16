namespace RKamphorst.PluginLoading.Aws;

public interface IPluginLibraryTimestampProvider
{
    Task<DateTimeOffset?> GetTimestampAsync(CancellationToken cancellationToken);
}