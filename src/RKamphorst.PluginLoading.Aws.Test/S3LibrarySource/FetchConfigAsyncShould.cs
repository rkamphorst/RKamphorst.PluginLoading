using Amazon.S3;
using FluentAssertions;
using Moq;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

using S3LibrarySource = RKamphorst.PluginLoading.Aws.S3LibrarySource;

public class FetchConfigAsyncShould
{
    [Fact]
    public async Task FetchStreamFromCorrectKey()
    {
        var s3ClientMock = new Mock<IAmazonS3>();

        var stream = new MemoryStream();
        s3ClientMock
            .Setup(m => m.GetObjectStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream)stream);
                
        
        var sut = new S3LibrarySource(new S3LibrarySourceOptions
        {
            Bucket = "bucket",
            Prefix = "prefix"
        }, s3ClientMock.Object);


        var result = await sut.FetchConfigAsync("name", CancellationToken.None);

        result.Should().BeSameAs(stream);
        s3ClientMock.Verify(m => m.GetObjectStreamAsync(
            "bucket",
            "prefix/name-dotnet-pluginconfig.json",
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        s3ClientMock
            .Verify(m => m.GetObjectStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}