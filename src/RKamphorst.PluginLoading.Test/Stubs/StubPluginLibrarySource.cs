using System.IO.Compression;
using Moq;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.Test.Stubs;

public class StubPluginLibrarySource: IPluginLibrarySource
{
    public static Mock<StubPluginLibrarySource> CreateMock(string name) =>
        new(MockBehavior.Loose, name)
        {
            CallBase = true
        };

    private readonly List<string> _localLibraries;

    public StubPluginLibrarySource(string name)
    {
        _localLibraries = new List<string>();
        Name = name;
    }

    public void AddLocalLibrary(string name)
    {
        _localLibraries.Add(name);
    }

    public string Name { get; }
    
    public virtual Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_localLibraries.Select(l => new PluginLibraryReference
        {
            Name = l,
            Source = this
        }));
    }

    public virtual Task<Stream> FetchCodeZipAsync(string name, CancellationToken cancellationToken)
    {
        var assemblyPath = StubPluginLibraries.GetPathToAssemblyFolder(name);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            CreateZipEntryFromDirectory(archive, assemblyPath);
        }
        
        stream.Seek(0, SeekOrigin.Begin);
        return Task.FromResult((Stream) stream);
    }

    public virtual Task<Stream?> FetchConfigAsync(string name, CancellationToken cancellationToken)
    {
        return Task.FromResult((Stream?) null);
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