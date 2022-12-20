using System.IO.Compression;
using System.Text;
using RKamphorst.PluginLoading.Contract;

namespace Sample.Api;

public class PluginLibrarySource: IPluginLibrarySource
{
    private readonly ILogger<PluginLibrarySource> _logger;

    static string GetPathToAssemblyFolder(string name)
    {
        var thisType = typeof(PluginLibrarySource);
        string assemblyName = thisType.Assembly.GetName().Name!;
        string assemblyPath = Path.GetDirectoryName(thisType.Assembly.Location)!;

        var segments = new List<string>();
        do
        {
            segments.Add(Path.GetFileName(assemblyPath));
            assemblyPath = Path.GetDirectoryName(assemblyPath)!;
        } while (segments[^1] != assemblyName);

        segments.Reverse();
        segments.RemoveAt(0);

        assemblyPath = Path.Combine(
            new[] { assemblyPath }
                .Concat(new[] { name })
                .Concat(segments)
                .ToArray()
        );
        return assemblyPath;
    }
    
    public PluginLibrarySource(string name, ILogger<PluginLibrarySource> logger)
    {
        _logger = logger;
        Name = name;
    }

    public string Name { get; }

    public virtual Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting list of libraries");
        return Task.FromResult((IEnumerable<PluginLibraryReference>)new[]
        {
            new PluginLibraryReference()
            {
                Name = "Sample.Plugin.A",
                Source = this
            }
        });
    }

    public virtual Task<Stream> FetchCodeZipAsync(string name, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching code for {LibraryName}", name);
        var assemblyPath = GetPathToAssemblyFolder(name);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            CreateZipEntryFromDirectory(archive, assemblyPath);
        }
        
        stream.Seek(0, SeekOrigin.Begin);
        return Task.FromResult((Stream) stream);
    }

    public virtual Task<Stream> FetchConfigAsync(string name, CancellationToken cancellationToken)
    {
        var result = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        result.Seek(0, SeekOrigin.Begin);
        return Task.FromResult((Stream) result);
    }
    
    private static void CreateZipEntryFromAny(ZipArchive archive, string sourceName, string entryName = "")
    {
        var fileName = Path.GetFileName(sourceName);
        if (File.GetAttributes(sourceName).HasFlag(FileAttributes.Directory))
        {
            CreateZipEntryFromDirectory(archive, sourceName, Path.Combine(entryName, fileName));
        }
        else
        {
            archive.CreateEntryFromFile(sourceName, Path.Combine(entryName, fileName), CompressionLevel.Fastest);
        }
    }

    private static void CreateZipEntryFromDirectory(ZipArchive archive, string sourceDirName, string entryName = "")
    {
        string[] files = Directory.GetFiles(sourceDirName).Concat(Directory.GetDirectories(sourceDirName)).ToArray();
        archive.CreateEntry(Path.Combine(entryName, Path.GetFileName(sourceDirName)));
        foreach (var file in files)
        {
            CreateZipEntryFromAny(archive, file, entryName);
        }
    }
}