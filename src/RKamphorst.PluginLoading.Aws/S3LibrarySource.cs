using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.Aws;

public class S3LibrarySource : IPluginLibrarySource
{
    private const string DotNetZipPostfix = "-dotnet.zip";
    private const string DotNetConfigPostfix = "-dotnet-pluginconfig.json";

    private readonly ILogger<S3LibrarySource> _logger;
    private readonly IAmazonS3 _client;
    private readonly string _prefix;
    private readonly string _bucket;

    public S3LibrarySource(S3LibrarySourceOptions? options, ILogger<S3LibrarySource> logger)
        : this(options, logger, new AmazonS3Client())
    {
    }

    public S3LibrarySource(S3LibrarySourceOptions? options, ILogger<S3LibrarySource> logger, IAmazonS3 client)
    {
        _bucket = !string.IsNullOrWhiteSpace(options?.Bucket?.Trim())
            ? options.Bucket.Trim()
            : throw new ArgumentException("Bucket name is empty", nameof(options));
        
        _prefix = !string.IsNullOrWhiteSpace(options.Prefix?.Trim())
            ? options.Prefix.Trim().EndsWith("/") ? options.Prefix.Trim() : $"{options.Prefix.Trim()}/"
            : "";

        _logger = logger;
        _client = client;
    }

    public string Name => $"s3://{_bucket}/{_prefix}";
    
    public async Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken)
    {
        var result = new List<PluginLibraryReference>();
        string? continuationToken = null;

        do
        {
            _logger.LogInformation(
                "Listing objects in {PluginLibrarySourceS3Url}{ListPluginLibrariesContinuation}",
                $"s3://{_bucket}/{_prefix}", continuationToken != null ? " [continuation]" : null
                );
            
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = _prefix,
                ContinuationToken = continuationToken,
            }, cancellationToken);

            var foundLibraries =
                response.S3Objects
                    .Select(o => ParseLibraryNameFromCodeZipKey(o.Key))
                    .Where(name => name != null)
                    .Select(name => new PluginLibraryReference
                    {
                        Name = name!,
                        Source = this
                    }).ToArray();
            
            result.AddRange(foundLibraries);
            
            _logger.LogDebug(
                "Found {PluginLibrariesCount} libraries",
                foundLibraries.Length
            );
            continuationToken = response.NextContinuationToken;
            
        } while (continuationToken != null);
        
        return result;
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
    
    public async Task<Stream> FetchCodeZipAsync(string name, CancellationToken cancellationToken)
    {
        var key = $"{_prefix}{name}{DotNetZipPostfix}";
        _logger.LogInformation(
            "Downloading library code zip from {PluginLibraryCodeS3Url}",
            $"s3://{_bucket}/{key}"
        );
        return await _client.GetObjectStreamAsync(
            _bucket, key, 
            null, cancellationToken);
    }

    public async Task<Stream?> FetchConfigAsync(string name, CancellationToken cancellationToken)
    {
        var key = $"{_prefix}{name}{DotNetConfigPostfix}";
        try
        {
            _logger.LogInformation(
                "Trying to download library config from {PluginLibraryConfigS3Url}",
                $"s3://{_bucket}/{key}"
            );
            return await _client.GetObjectStreamAsync(
                _bucket, key,
                null, cancellationToken);

        }
        catch (AmazonS3Exception ex) 
            when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NoSuchKey")
        {
            _logger.LogInformation(
                "No library config found at {PluginLibraryConfigS3Url}",
                $"s3://{_bucket}/{key}"
            );
            return null;
        }
    }
}