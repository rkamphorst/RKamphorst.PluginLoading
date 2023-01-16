using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Aws.Options;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.Aws;

public class S3LibrarySource : IPluginLibrarySource, IPluginLibraryTimestampProvider
{
    private const string DotNetZipPostfix = "-dotnet.zip";
    private const string DotNetConfigPostfix = $"-dotnet-pluginsettings.json";

    private readonly ILogger<S3LibrarySource> _logger;
    private readonly IAmazonS3 _client;
    private readonly string _prefix;
    private readonly string _bucket;
    private readonly DateTimeOffset? _versionAtDate;

    public S3LibrarySource(S3LibrarySourceOptions? options, ILogger<S3LibrarySource> logger)
        : this(options, logger, new AmazonS3Client())
    {
    }

    public S3LibrarySource(S3LibrarySourceOptions? options, ILogger<S3LibrarySource> logger, IAmazonS3 client)
    {
        _bucket = !string.IsNullOrWhiteSpace(options?.S3Bucket?.Trim())
            ? options.S3Bucket.Trim()
            : throw new ArgumentException("Bucket name is empty", nameof(options));
        
        _prefix = !string.IsNullOrWhiteSpace(options.S3Prefix?.Trim())
            ? options.S3Prefix.Trim().EndsWith("/") ? options.S3Prefix.Trim() : $"{options.S3Prefix.Trim()}/"
            : "";
        _logger = logger;
        _client = client;

        _versionAtDate = options.VersionAtDate;
        if (_versionAtDate.HasValue)
        {
            _logger.LogInformation(
                "Using version at date from settings: {VersionAtDate}", _versionAtDate.Value
                );
        } else if (
            !string.IsNullOrWhiteSpace(options.VersionAtDateEnvironmentVariable)
            && DateTimeOffset.TryParse(
                Environment.GetEnvironmentVariable(options.VersionAtDateEnvironmentVariable),
                out DateTimeOffset dt
            )
           )
        {
            _logger.LogInformation(
                "Using version at date from {VersionAtDateEnvironmentVariable}: {VersionAtDate}",
                options.VersionAtDateEnvironmentVariable, dt);
            _versionAtDate = dt;
        }
        else
        {
            _logger.LogInformation(
                "Version at date not set, using {VersionAtDate}; which means latest version",
                 (DateTimeOffset?)null);
            
        }

    }

    public string Name => $"s3://{_bucket}/{_prefix}";
    
    public async Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken)
    {
        return await
            ListS3CodeAndConfigAsync(null, cancellationToken)
                .Select(codeAndConfig => new PluginLibraryReference
                {
                    Name = codeAndConfig.Name,
                    Source = this
                })
                .ToListAsync(cancellationToken);
    }

    public async Task<Stream> FetchCodeZipAsync(string name, CancellationToken cancellationToken)
    {
        S3CodeAndConfig s3CodeAndConfig =
            await GetS3CodeAndConfigAsync(name, cancellationToken);

        var codeZip = s3CodeAndConfig.CodeZip;
        
        _logger.LogDebug(
            "Downloading library code zip from {CodeS3Url} (version of {CodeS3Timestamp:O})",
            $"s3://{_bucket}/{codeZip.Key}", codeZip.LastModified
        );
        
        GetObjectResponse objectResponse = await _client.GetObjectAsync(_bucket, codeZip.Key, codeZip.VersionId, cancellationToken);

        return objectResponse.ResponseStream;
    }

    public async Task<Stream> FetchConfigAsync(string name, CancellationToken cancellationToken)
    {
        S3CodeAndConfig s3CodeAndConfig =
            await GetS3CodeAndConfigAsync(name, cancellationToken);

        var config = s3CodeAndConfig.Config;
        
        _logger.LogDebug(
            "Downloading library config from {ConfigS3Url} (version of {ConfigS3Timestamp:O})",
            $"s3://{_bucket}/{config.Key}", config.LastModified
        );
        
        GetObjectResponse objectResponse = await _client.GetObjectAsync(_bucket, config.Key, config.VersionId, cancellationToken);

        return objectResponse.ResponseStream;
    }

    public async Task<DateTimeOffset?> GetTimestampAsync(CancellationToken cancellationToken)
    {
        return await
            ListS3CodeAndConfigAsync(null, cancellationToken)
                .MaxAsync(o => (DateTimeOffset?)o.Timestamp, cancellationToken);
    }

    private async Task<S3CodeAndConfig> GetS3CodeAndConfigAsync(string name, CancellationToken cancellationToken)
    {
        S3CodeAndConfig? s3CodeAndConfig = 
            await ListS3CodeAndConfigAsync(name, cancellationToken)
                .SingleOrDefaultAsync(cancellationToken);

        if (s3CodeAndConfig == null)
        {
            throw new InvalidOperationException($"Library {name} not complete or not found");
        }

        _logger.LogDebug(
            "Got code (@ {CodeZipTimestamp:yyyy-M-dTHH:mm:ssZ}) and " +
            "config (@{ConfigTimestamp:yyyy-M-dTHH:mm:ssZ}) for library {LibraryName}",
            s3CodeAndConfig.CodeZip.LastModified,
            s3CodeAndConfig.Config.LastModified,
            name
        );

        return s3CodeAndConfig;
    }

    private IAsyncEnumerable<S3CodeAndConfig> ListS3CodeAndConfigAsync(string? name, CancellationToken cancellationToken)
    {
        return
            ListObjectVersionsUntilAtDate(name, cancellationToken)
                .Select(v => new S3CodeOrConfig(
                    ParseLibraryNameFromConfigKey(v.Key),
                    ParseLibraryNameFromCodeZipKey(v.Key),
                    v)
                )
                .GroupBy(o => o.Name)
                .Select(g => S3CodeAndConfig.Create(g.ToEnumerable()))
                .Where(o => o != null)
                .Select(o => o!);
    }

    private async IAsyncEnumerable<S3ObjectVersion> ListObjectVersionsUntilAtDate(string? name, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? keyMarker = null;
        string? versionIdMarker = null;
        var continuationCounter = 0;
        ListVersionsResponse? response;
        var prefixOrKey = name == null ? $"{_prefix}" : $"{_prefix}{name}";
            
        do
        {
            _logger.LogDebug(
                "Listing versions for {PluginLibrarySourceS3Url} [{ContinuationCounter}] (at date: {VersionAtDate:O})",
                $"s3://{_bucket}/{prefixOrKey}", continuationCounter, _versionAtDate
            );
            
            response = await _client.ListVersionsAsync(new ListVersionsRequest
            {
                BucketName = _bucket,
                Prefix = prefixOrKey,
                KeyMarker = keyMarker,
                VersionIdMarker = versionIdMarker
            }, cancellationToken);

            foreach (S3ObjectVersion objectVersion in 
                        response.Versions
                            .Where(v => !_versionAtDate.HasValue || v.LastModified <= _versionAtDate)) 
            {
                yield return objectVersion;
            }
            
            keyMarker = response.NextKeyMarker;
            versionIdMarker = response.NextVersionIdMarker;
            continuationCounter++;
        } while (response.IsTruncated);
    }
    
    private string? ParseLibraryNameFromCodeZipKey(string key)
    {
        if (key.StartsWith(_prefix))
        {
            var zipName = key[_prefix.Length..];
            if (!zipName.Contains('/') && zipName.EndsWith(DotNetZipPostfix))
            {
                return zipName[..^DotNetZipPostfix.Length];
            }
        }
        return null;
    }
    
    private string? ParseLibraryNameFromConfigKey(string key)
    {
        if (key.StartsWith(_prefix))
        {
            var name = key[_prefix.Length..];
            if (!name.Contains('/') && name.EndsWith(DotNetConfigPostfix))
            {
                return name[..^DotNetConfigPostfix.Length];
            }
        }
        return null;
    }
    
    private class S3CodeAndConfig
    {
        public static S3CodeAndConfig? Create(IEnumerable<S3CodeOrConfig> group)
        {
            string? name = null;
            S3ObjectVersion? config = null;

            foreach (S3CodeOrConfig item in group
                        .Where(i => i.IsCode || i.IsConfig)
                        .OrderByDescending(o => o.Timestamp) // newest
                        .ThenBy(o => o.IsConfig ? 0 : 1) // then, config objects first
            )
            {
                if (item.ObjectVersion.IsDeleteMarker)
                {
                    return null;
                }
                
                if (item.IsConfig && config == null)
                {
                    name = item.Name;
                    config = item.ObjectVersion;
                } 
                else if (item.IsCode && config != null)
                {
                    S3ObjectVersion codeZip = item.ObjectVersion;
                    return new S3CodeAndConfig(name!, config, codeZip);
                } 
            }

            return null;
        }

        private S3CodeAndConfig(string name, S3ObjectVersion config, S3ObjectVersion codeZip)
        {
            Name = name;
            CodeZip = codeZip;
            Config = config;
        }
 
        public string Name { get; }

        public S3ObjectVersion Config { get; }
        
        public S3ObjectVersion CodeZip { get; }

        public DateTimeOffset Timestamp =>
            Config.LastModified.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(Config.LastModified, TimeSpan.Zero)
                : new DateTimeOffset(Config.LastModified);
    }
    
    private class S3CodeOrConfig
    {
        public S3CodeOrConfig(string? configName, string? codeZipName, S3ObjectVersion objectVersion)
        {
            ObjectVersion = objectVersion;
            Name = configName ?? codeZipName;
            IsConfig = configName != null;
            IsCode = codeZipName != null;
        }
        
        public string? Name { get; }

        public bool IsConfig { get; }

        public bool IsCode { get; }
        
        public S3ObjectVersion ObjectVersion { get; }
        
        public DateTimeOffset Timestamp =>
            ObjectVersion.LastModified.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(ObjectVersion.LastModified, TimeSpan.Zero)
                : new DateTimeOffset(ObjectVersion.LastModified);
        
    }
}