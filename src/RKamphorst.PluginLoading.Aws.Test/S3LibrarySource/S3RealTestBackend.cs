using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Aws.Options;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

using S3LibrarySource = Aws.S3LibrarySource;

public class S3RealTestBackend : IS3TestBackend
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucket;

    public S3RealTestBackend(IAmazonS3 s3Client, string bucket)
    {
        _s3Client = s3Client;
        _bucket = bucket;
    }
    
    public async Task<DateTimeOffset> Setup(string prefix, DateTimeOffset referenceDateTime, List<(S3ObjectVersion Version, string? Content)> versionsAndContent)
    {
        await Cleanup(prefix);
        return await CreateObjects(referenceDateTime, versionsAndContent);
    }

    private async Task Cleanup(string prefix)
    {
        ListVersionsResponse? listVersionsResponse;
        do
        {
            listVersionsResponse = await _s3Client.ListVersionsAsync(new ListVersionsRequest
            {
                BucketName = _bucket,
                KeyMarker = null,
                VersionIdMarker = null,
                Prefix = prefix,
            });
            
            var toDelete = listVersionsResponse.Versions
                .Select(v => new KeyVersion { Key = v.Key, VersionId = v.VersionId })
                .ToList();
            var retryCount = 0;
            while (toDelete?.Count > 0)
            {
                if (retryCount > 0)
                {
                    await Task.Delay(100);
                }

                retryCount++;
                
                var deleteObjectsResponse = await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucket,
                    Objects = toDelete,
                    Quiet = true
                });
                
                toDelete = deleteObjectsResponse
                    ?.DeleteErrors
                    ?.Select(e => new KeyVersion { Key = e.Key, VersionId = e.VersionId }).ToList();

                if (retryCount > 5 && toDelete?.Count > 0)
                {
                    throw new Exception("Retried too many times");
                }

            }


        } while (listVersionsResponse.Versions.Count > 0);
    }

    private async Task<DateTimeOffset> CreateObjects(DateTimeOffset referenceDateTime,
        List<(S3ObjectVersion Version, string? Content)> versionsAndContent
        )
    {
        var groups = versionsAndContent
            .GroupBy(tuple => tuple.Version.LastModified)
            .OrderBy(g => g.Key);
        
        DateTime? lastGroupKey = null;
        string? lastKey = null;
        string? lastVersion = null;
        DateTimeOffset? resultTimestamp = null;
        foreach (var group in groups)
        {
            if (lastGroupKey.HasValue)
            {
                TimeSpan delay = group.Key - lastGroupKey.Value;
                if (delay.TotalSeconds < 1)
                {
                    delay = TimeSpan.FromSeconds(1);
                }
                await Task.Delay(delay);
            }

            var keysAndVersions = await Task.WhenAll(group.Select(PutVersion));
            var last = keysAndVersions.Last();
            lastKey = last?.Key;
            lastVersion = last?.VersionId;
            
            if (group.Key > referenceDateTime && !resultTimestamp.HasValue)
            {
                resultTimestamp = (await GetTimestamp(lastKey!, lastVersion!)) - (group.Key - referenceDateTime);
            }
            
            lastGroupKey = group.Key;
        }

        if (resultTimestamp.HasValue)
        {
            return resultTimestamp.Value;
        }

        if (lastKey != null && lastVersion != null && lastGroupKey.HasValue)
        {
            return (await GetTimestamp(lastKey, lastVersion)) + (referenceDateTime - lastGroupKey.Value);
        }
        
        return DateTimeOffset.UtcNow;

        async Task<(string Key, string VersionId)?> PutVersion((S3ObjectVersion Version, string? Content) tpl)
        {
            if (tpl.Version.IsDeleteMarker)
            {
                var deleteResponse = await _s3Client.DeleteObjectAsync(new DeleteObjectRequest()
                {
                    BucketName = _bucket,
                    Key = tpl.Version.Key,
                });
                return (tpl.Version.Key, deleteResponse.VersionId);    
            }
            var putResponse = await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                ContentBody = tpl.Content ?? $"{tpl.Version.Key}-content",
                Key = tpl.Version.Key,
            });
            return (tpl.Version.Key, putResponse.VersionId);
        }
        
        async Task<DateTimeOffset> GetTimestamp(string key, string version)
        {
            ListVersionsResponse? listVersionsResponse;
            string? keyMarker = null;
            string? versionMarker = null; 
            do
            {

                listVersionsResponse = await _s3Client.ListVersionsAsync(new ListVersionsRequest()
                {
                    BucketName = _bucket,
                    Prefix = key,
                    KeyMarker = keyMarker,
                    VersionIdMarker = versionMarker
                });

                var objectVersion = listVersionsResponse.Versions.FirstOrDefault(v => v.VersionId == version);
                if (objectVersion != null)
                {
                    return objectVersion.LastModified;
                }

                keyMarker = listVersionsResponse.NextKeyMarker;
                versionMarker = listVersionsResponse.NextVersionIdMarker;
            } while (listVersionsResponse.IsTruncated);

            throw new InvalidOperationException();
        }
        
    }
    
    public S3LibrarySource CreateSystemUnderTest(string prefix, DateTimeOffset? versionAtDate = null)
    {
        return new S3LibrarySource(new S3LibrarySourceOptions
        {
            S3Bucket = _bucket,
            S3Prefix = prefix,
            VersionAtDate = versionAtDate
        }, Mock.Of<ILogger<S3LibrarySource>>(), _s3Client);
    }

    public void VerifyAll()
    {
        /* skip */
    }
}