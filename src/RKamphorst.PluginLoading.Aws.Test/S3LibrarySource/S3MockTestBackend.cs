using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Aws.Options;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

using S3LibrarySource = Aws.S3LibrarySource;

public class S3MockTestBackend : IS3TestBackend
{
    private readonly Mock<IAmazonS3> _s3ClientMock;
    public S3MockTestBackend()
    {
        _s3ClientMock = new Mock<IAmazonS3>(MockBehavior.Strict);
    }
    public Task<DateTimeOffset> Setup(string prefix, DateTimeOffset referenceDateTime, List<(S3ObjectVersion Version, string? Content)> versionsAndContent)
    {
        SetupListVersionsRequests(versionsAndContent.Select(v => v.Version));
        SetupGetObjectRequests(versionsAndContent);

        return Task.FromResult(referenceDateTime);
    }

    private void SetupGetObjectRequests(List<(S3ObjectVersion Version, string? Content)> versionsAndContent)
    {
        foreach (var tuple in versionsAndContent.Where(v => v.Content != null))
        {
            _s3ClientMock
                .Setup(m => m.GetObjectAsync(It.IsAny<string>(), tuple.Version.Key, tuple.Version.VersionId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse
                {
                    ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(tuple.Content!)),
                    LastModified = tuple.Version.LastModified,
                    VersionId = tuple.Version.VersionId,
                    Key = tuple.Version.Key,
                }).Verifiable();
        }
    }

    private void SetupListVersionsRequests(IEnumerable<S3ObjectVersion> objectVersions)
    {
        var objectVersionsList = objectVersions.ToList();
        const int batchSize = 1000;

        for (int i = 0; i < objectVersionsList.Count; i += batchSize)
        {
            var batch =
                i + batchSize >= objectVersionsList.Count
                    ? new List<S3ObjectVersion>(objectVersionsList)
                    : objectVersionsList.GetRange(i, batchSize);
            var keyMarker = i == 0 ? null : $"k{i}";
            var versionIdMarker = i == 0 ? null : $"v{i}";
            var nextKeyMarker = i + batchSize >= objectVersionsList.Count ? null : $"k{i+batchSize}";
            var nextVersionIdMarker = i + batchSize >= objectVersionsList.Count ? null : $"v{i+batchSize}";
            var isTruncated = nextKeyMarker != null;

            _s3ClientMock
                .Setup(m => m.ListVersionsAsync(
                    It.Is<ListVersionsRequest>(r => r.KeyMarker == keyMarker && r.VersionIdMarker == versionIdMarker),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new ListVersionsResponse
                    {
                        Versions = batch.ToList(),
                        NextKeyMarker = nextKeyMarker,
                        NextVersionIdMarker = nextVersionIdMarker,
                        IsTruncated = isTruncated
                    }
                ).Verifiable();
        }
        
        
    }

    public S3LibrarySource CreateSystemUnderTest(string prefix, DateTimeOffset? versionAtDate)
    {
        return new S3LibrarySource(new S3LibrarySourceOptions
        {
            S3Bucket = "bucket",
            S3Prefix = prefix,
            VersionAtDate = versionAtDate
        }, Mock.Of<ILogger<S3LibrarySource>>(), _s3ClientMock.Object);
    }
    
    public void VerifyAll()
    {
        _s3ClientMock.VerifyAll();
    }
}