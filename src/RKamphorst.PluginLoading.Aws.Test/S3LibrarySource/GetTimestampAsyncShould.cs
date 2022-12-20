using FluentAssertions;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

public class GetTimestampAsyncShould
{
    private readonly IS3TestBackend _backend;
    
    public GetTimestampAsyncShould()
    {
        _backend = S3TestBackendExtensions.CreateBackend();
    }
    
    [Fact]
    public async Task ReportLatestTimestamp()
    {
        string prefix = $"{GetType().Name}_{nameof(ReportLatestTimestamp)}/";
        var versions = _backend.GenerateVersions(0, 10,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(-n-1)
            )
            .Concat(
                _backend.GenerateVersions(0, 10,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: n => TimeSpan.FromMilliseconds(-n)
                )
            )
            .Shuffle().ToList();
        
        var referenceDateTime = await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetTimestampAsync(CancellationToken.None));
        result.Should().BeOnOrBefore(referenceDateTime);
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task ReportLatestTimestampBeforeVersionAtDate()
    {
        string prefix = $"{GetType().Name}_{nameof(ReportLatestTimestampBeforeVersionAtDate)}/";
        var versions = _backend.GenerateVersions(0, 10,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(5-n-1)
            )
            .Concat(
                _backend.GenerateVersions(0, 10,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: n => TimeSpan.FromMilliseconds(5-n)
                )
            )
            .Shuffle().ToList();
        
        var referenceDateTime = await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix, referenceDateTime);

        var result = (await sut.GetTimestampAsync(CancellationToken.None));
        result.Should().BeOnOrBefore(referenceDateTime);
        _backend.VerifyAll();
    }
}