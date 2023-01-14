using System.Text;
using FluentAssertions;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

public class FetchConfigAsyncShould
{
    private const string DotNetZipPostfix = "-dotnet.zip";
    private const string DotNetConfigPostfix = $"-dotnet-pluginsettings.json";
    
    private readonly IS3TestBackend _backend;

    public FetchConfigAsyncShould()
    {
        _backend = S3TestBackendExtensions.CreateBackend();
    }

    [Fact]
    public async Task FetchConfigStreamFromCorrectKey()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchConfigStreamFromCorrectKey)}/";
        var versions = _backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name{DotNetZipPostfix}",
                lastModifiedOffset: _ => TimeSpan.Zero
            )
            .Concat(_backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name{DotNetConfigPostfix}",
                lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1),
                content: _ => "config"
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        await using Stream s = await sut.FetchConfigAsync("name", CancellationToken.None);
        using var sr = new StreamReader(s, Encoding.UTF8);

        var result = await sr.ReadToEndAsync();

        result.Should().Be("config");
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task FetchConfigStreamFromLatestVersion()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchConfigStreamFromLatestVersion)}/";
        var versions = _backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name{DotNetZipPostfix}",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n)
            )
            .Concat(_backend.GenerateVersions(
                1, 5,
                keys: _ => $"{prefix}name{DotNetConfigPostfix}",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n),
                content: n => n == 5 ? "latestVersion" : null
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        await using Stream s = await sut.FetchConfigAsync("name", CancellationToken.None);
        using var sr = new StreamReader(s, Encoding.UTF8);

        var result = await sr.ReadToEndAsync();

        result.Should().Be("latestVersion");
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task ThrowExceptionIfDeleted()
    {
        string prefix = $"{GetType().Name}_{nameof(ThrowExceptionIfDeleted)}/";
        var versions = _backend.GenerateVersions(
                0, 1,
                keys: _ => $"{prefix}name{DotNetZipPostfix}",
                lastModifiedOffset: _ => TimeSpan.Zero
            )
            .Concat(_backend.GenerateVersions(
                0, 2,
                keys: _ => $"{prefix}name{DotNetConfigPostfix}",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n),
                isDeleted: n => n == 1
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var act = () => sut.FetchConfigAsync("name", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _backend.VerifyAll();
    }
}