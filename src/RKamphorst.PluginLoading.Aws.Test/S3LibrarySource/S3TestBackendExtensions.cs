using Amazon.S3.Model;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

public static class S3TestBackendExtensions
{

    public static IS3TestBackend CreateBackend()
    {
        // testing with real aws bucket can be done as follows: 
        // AWSConfigs.AWSProfileName = "[your-profile]";
        // return new S3RealTestBackend(new AmazonS3Client(), "[your-bucket]");
        
        return new S3MockTestBackend();
    }
    
    public static DateTimeOffset ReferenceDateTime(this IS3TestBackend _) => DateTimeOffset.Parse("1982-03-18T02:15:55Z");

    public static IEnumerable<(S3ObjectVersion Version, string? Content)> GenerateVersions(
        this IS3TestBackend self,
        int start, int count, 
        Func<int, string>? keys = null, 
        Func<int, TimeSpan>? lastModifiedOffset = null,
        Func<int, bool>? isDeleted = null,
        Func<int, string?>? content = null,
        DateTimeOffset? referenceDateTime = null
    ) =>
        Enumerable.Range(start, count).Select(n => (
            Version: new S3ObjectVersion
            {
                Key = keys?.Invoke(n) ?? "key",
                LastModified = (referenceDateTime ?? ReferenceDateTime(self)).Add(lastModifiedOffset?.Invoke(n) ?? TimeSpan.Zero).UtcDateTime,
                IsDeleteMarker = isDeleted?.Invoke(n) ?? false
            }, 
            Content: content?.Invoke(n) 
        ));
}