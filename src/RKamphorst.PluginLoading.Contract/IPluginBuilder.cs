namespace RKamphorst.PluginLoading.Contract;

public interface IPluginBuilder
{
    
    Task<IEnumerable<Plugin>> BuildAsync(CancellationToken cancellationToken);
}