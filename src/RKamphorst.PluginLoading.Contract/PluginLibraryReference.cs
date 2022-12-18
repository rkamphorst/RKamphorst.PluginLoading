namespace RKamphorst.PluginLoading.Contract;

public readonly struct PluginLibraryReference
{
    public string Name { get; init;  }
    
    public IPluginLibrarySource Source { get; init;  }

    public Task<Stream> FetchCodeZipAsync(CancellationToken cancellationToken)
        => Source.FetchCodeZipAsync(Name, cancellationToken);

    public Task<Stream?> FetchConfigAsync(CancellationToken cancellationToken)
        => Source.FetchConfigAsync(Name, cancellationToken);

    public override string ToString()
    {
        return $"{Source.Name}::{Name}";
    }

    
}

