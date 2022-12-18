using System.IO.Compression;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public class PluginStore : IPluginStore
{
    private string? _tempDirectory;
    
    public async Task<string> GetPathToLibraryAssemblyAsync(PluginLibraryReference lib, CancellationToken cancellationToken)
    {
        if (_tempDirectory == null)
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
        }

        var pluginDirectory = Path.Combine(_tempDirectory, lib.Source.Name, lib.Name);
        if (!Directory.Exists(pluginDirectory))
        {
            Directory.CreateDirectory(pluginDirectory);
            try
            {
                await StorePluginLibraryCodeAsync(lib, pluginDirectory, cancellationToken);
                await StorePluginLibraryConfigAsync(lib, pluginDirectory, cancellationToken);
            }
            catch
            {
                Directory.Delete(pluginDirectory, recursive: true);
                throw;
            }
        }

        var assemblyPath = Path.Combine(pluginDirectory, $"{lib.Name}.dll");
        var depsPath = Path.Combine(pluginDirectory, $"{lib.Name}.deps.json");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                $"Plugin library {lib} was stored but assembly file not found at location {assemblyPath}");
        }

        if (!File.Exists(depsPath))
        {
            throw new InvalidOperationException(
                $"Plugin library {lib} was stored but deps file not found at location {depsPath}");
        }

        return assemblyPath;
    }

    private async Task StorePluginLibraryCodeAsync(PluginLibraryReference lib, string toDirectory,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await lib.FetchCodeZipAsync(cancellationToken);
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        zipArchive.ExtractToDirectory(toDirectory);
    }

    private async Task StorePluginLibraryConfigAsync(PluginLibraryReference lib, string toDirectory,
        CancellationToken cancellationToken)
    {
        var destPath = Path.Combine(toDirectory, RKamphorst.PluginConfiguration.Contract.Convention.ConfigurationFileName);
        if (File.Exists(destPath))
        {
            File.Delete(destPath);
        }

        try
        {
            Stream? stream = await lib.FetchConfigAsync(cancellationToken);
            if (stream != null)
            {
                await using (stream)
                {
                    await using FileStream dest = File.OpenWrite(destPath);
                    await stream.CopyToAsync(dest, cancellationToken);
                }
            }
        }
        catch
        {
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            throw;
        }
    }
}