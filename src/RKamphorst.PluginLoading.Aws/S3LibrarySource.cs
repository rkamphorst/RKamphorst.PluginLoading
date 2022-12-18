using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading.Aws;

public class S3LibrarySource : IPluginLibrarySource
{
    private const string DotNetZipPostfix = "-dotnet.zip";
    private const string DotNetConfigPostfix = "-dotnet-pluginconfig.json";
    
    private readonly IAmazonS3 _client;
    private readonly string _prefix;
    private readonly string _bucket;

    public S3LibrarySource(S3LibrarySourceOptions? options)
        : this(options, new AmazonS3Client())  {}

    public S3LibrarySource(S3LibrarySourceOptions? options, IAmazonS3 client)
    {
        _bucket = !string.IsNullOrWhiteSpace(options?.Bucket?.Trim())
            ? options.Bucket.Trim()
            : throw new ArgumentException("Bucket name is empty", nameof(options));
        
        _prefix = !string.IsNullOrWhiteSpace(options.Prefix?.Trim())
            ? options.Prefix.Trim().EndsWith("/") ? options.Prefix.Trim() : $"{options.Prefix.Trim()}/"
            : ""; 
                
        _client = client;
    }

    public string Name => $"s3://{_bucket}/{_prefix}";
    
    public async Task<IEnumerable<PluginLibraryReference>> GetListAsync(CancellationToken cancellationToken)
    {
        var result = new List<PluginLibraryReference>();
        string? continuationToken = null;

        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = _prefix,
                ContinuationToken = continuationToken,
            }, cancellationToken);

            result.AddRange(
                response.S3Objects
                    .Select(o => ParseLibraryNameFromCodeZipKey(o.Key))
                    .Where(name => name != null)
                    .Select(name => new PluginLibraryReference
                    {
                        Name = name!,
                        Source = this
                    })
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
        return await _client.GetObjectStreamAsync(
            _bucket, $"{_prefix}{name}{DotNetZipPostfix}", 
            null, cancellationToken);
    }

    public async Task<Stream?> FetchConfigAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetObjectStreamAsync(
                _bucket, $"{_prefix}{name}{DotNetConfigPostfix}",
                null, cancellationToken);

        }
        catch (AmazonS3Exception ex) 
            when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }
}