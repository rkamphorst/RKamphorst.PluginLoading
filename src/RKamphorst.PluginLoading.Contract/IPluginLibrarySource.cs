namespace RKamphorst.PluginLoading.Contract;

public interface IPluginLibrarySource
{
    public string Name { get; }
    
    Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken);

    Task<Stream> FetchCodeZipAsync(string name, CancellationToken cancellationToken);

    Task<Stream> FetchConfigAsync(string name, CancellationToken cancellationToken);
}