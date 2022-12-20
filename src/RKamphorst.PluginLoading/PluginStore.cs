using System.IO.Compression;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public class PluginStore : IPluginStore
{
    private readonly ILogger<PluginStore> _logger;
    private string? _pluginStoreDirectory;

    public PluginStore(ILogger<PluginStore> logger)
    {
        _logger = logger;
    }
    
    public async Task<string> GetPathToLibraryAssemblyAsync(PluginLibraryReference lib, CancellationToken cancellationToken)
    {
        if (_pluginStoreDirectory == null)
        {
            _pluginStoreDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _logger.LogInformation("Creating plugin store directory {PluginStoreDirectory}", _pluginStoreDirectory);
            Directory.CreateDirectory(_pluginStoreDirectory);
        }

        var pluginDirectory = Path.Combine(_pluginStoreDirectory, lib.Source.Name, lib.Name);
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogInformation(
                "Creating directory {PluginLibraryDirectory} for library {PluginLibrary} and storing code + config",
                pluginDirectory, lib);
            
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
        else
        {
            _logger.LogDebug(
                "Directory {PluginLibraryDirectory} for library {PluginLibrary} already exists, not downloading",
                pluginDirectory, lib);
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

        _logger.LogInformation(
            "Downloading and unzipping code for library {PluginLibrary} into directory {PluginLibraryDirectory}",
            lib, toDirectory);
        
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        zipArchive.ExtractToDirectory(toDirectory);
    }

    private async Task StorePluginLibraryConfigAsync(PluginLibraryReference lib, string toDirectory,
        CancellationToken cancellationToken)
    {
        var destPath = Path.Combine(toDirectory, PluginConfiguration.Contract.Convention.ConfigurationFileName);
        if (File.Exists(destPath))
        {
            _logger.LogWarning(
                "Configuration file {PluginConfigFile} for library {PluginLibrary} already exists, deleting",
                destPath, lib);
            File.Delete(destPath);
        }

        try
        {
            Stream? stream = await lib.FetchConfigAsync(cancellationToken);

            if (stream != null)
            {
                _logger.LogInformation(
                    "Downloading config for library {PluginLibrary} to {PluginConfigFile}",
                    lib, destPath);

                await using (stream)
                {
                    await using FileStream dest = File.OpenWrite(destPath);
                    await stream.CopyToAsync(dest, cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning(
                    "No configuration found for library {PluginLibrary}",
                    lib);
            }
        }
        catch (Exception ex)
        {
            if (File.Exists(destPath))
            {
                _logger.LogWarning(ex,
                    "Exception occurred, deleting configuration file {PluginConfigFile}",
                    destPath);
                File.Delete(destPath);
            }
            throw;
        }
    }
}