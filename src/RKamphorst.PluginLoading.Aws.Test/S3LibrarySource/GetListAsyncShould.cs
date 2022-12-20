using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

using S3LibrarySource = RKamphorst.PluginLoading.Aws.S3LibrarySource;

public class GetListAsyncShould
{

    [Fact]
    public async Task ImplementContinuation()
    {
        var s3ClientMock = new Mock<IAmazonS3>();
        s3ClientMock
            .Setup(m => m.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(r => r.ContinuationToken == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new S3Object { Key = "prefix/a-dotnet.zip" },
                        new S3Object { Key = "prefix/b-dotnet.zip" },
                    },
                    NextContinuationToken = "1"
                }
            );
        s3ClientMock
            .Setup(m => m.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(r => r.ContinuationToken == "1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new S3Object { Key = "prefix/c-dotnet.zip" },
                        new S3Object { Key = "prefix/d-dotnet.zip" },
                        new S3Object { Key = "prefix/e-dotnet.zip" }
                    },
                    NextContinuationToken = "2"
                }
            );
        s3ClientMock
            .Setup(m => m.ListObjectsV2Async(
                It.Is<ListObjectsV2Request>(r => r.ContinuationToken == "2"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new S3Object { Key = "prefix/f-dotnet.zip" },
                        new S3Object { Key = "prefixxx/g-dotnet.zip" }
                    },
                    NextContinuationToken = null
                }
            );

        var sut = new S3LibrarySource(new S3LibrarySourceOptions
        {
            Bucket = "bucket",
            Prefix = "prefix"
        }, Mock.Of<ILogger<S3LibrarySource>>(), s3ClientMock.Object);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(new[] { "a", "b", "c", "d", "e", "f" });
    }

    [Theory]
    [MemberData(nameof(GetCorrectListTestCases))]
    public async Task ReturnCorrectList(string prefix, string[] objectKeys, string[] expectNames)
    {
        var s3ClientMock = new Mock<IAmazonS3>();
        s3ClientMock
            .Setup(m => m.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ListObjectsV2Response { S3Objects = objectKeys.Select(k => new S3Object { Key = k }).ToList() }
            );

        var sut = new S3LibrarySource(new S3LibrarySourceOptions
        {
            Bucket = "bucket",
            Prefix = prefix
        }, Mock.Of<ILogger<S3LibrarySource>>(), s3ClientMock.Object);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(expectNames);
    }

    public static IEnumerable<object[]> GetCorrectListTestCases()
    {
        yield return new object[]
            { "prefix", new[] { "prefix/a-dotnet.zip", "prefix/b-dotnet.zip" }, new[] { "a", "b" } };
        yield return new object[]
            { "prefix", new[] { "prefix/a-dotnet.zip", "prefix/b-dotnet.zip","prefix/c-dotnet-pluginconfig.json" }, new[] { "a", "b" } };
        yield return new object[]
            { "prefix", new[] { "prefix/a-dotnet.zip", "prefix/b-python.zip" }, new[] { "a"  } };
        yield return new object[]
            { "prefix", new[] { "prefix/a-dotnet.zip", "prefix/pfx/b-dotnet.zip" }, new[] { "a" } };
        yield return new object[]
            { "prefix", new[] { "prefix2/a-dotnet.zip", "prefix/b-dotnet.zip" }, new[] { "b" } };
        yield return new object[]
            { "prefix", Array.Empty<string>(), Array.Empty<string>() };

    }
}