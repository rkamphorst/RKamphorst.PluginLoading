using System.Text;
using FluentAssertions;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

public class FetchCodeZipAsyncShould
{
    private readonly IS3TestBackend _backend;

    public FetchCodeZipAsyncShould()
    {
        _backend = S3TestBackendExtensions.CreateBackend();
    }

    [Fact]
    public async Task FetchCodeStreamFromCorrectKey()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchCodeStreamFromCorrectKey)}/";
        var versions = _backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet.zip",
                lastModifiedOffset: _ => TimeSpan.Zero,
                content: _ => "code"
            )
            .Concat(_backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet-pluginconfig.json",
                lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1)
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        await using Stream s = await sut.FetchCodeZipAsync("name", CancellationToken.None);
        using var sr = new StreamReader(s, Encoding.UTF8);

        var result = await sr.ReadToEndAsync();

        result.Should().Be("code");
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task FetchCodeStreamFromLatestVersion()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchCodeStreamFromLatestVersion)}/";
        var versions = _backend.GenerateVersions(
                1, 5,
                keys: _ => $"{prefix}name-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n),
                content: n => n == 5 ? "latestVersion" : null
            )
            .Concat(_backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet-pluginconfig.json",
                lastModifiedOffset: _ => TimeSpan.FromMilliseconds(10)
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        await using Stream s = await sut.FetchCodeZipAsync("name", CancellationToken.None);
        using var sr = new StreamReader(s, Encoding.UTF8);

        var result = await sr.ReadToEndAsync();

        result.Should().Be("latestVersion");
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task FetchCodeStreamFromLatestVersionWithConfig()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchCodeStreamFromLatestVersionWithConfig)}/";
        var versions = _backend.GenerateVersions(
                1, 10,
                keys: _ => $"{prefix}name-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n*2),
                content: n => n == 5 ? "latestVersionWithConfig" : null
            )
            .Concat(_backend.GenerateVersions(
                1, 5,
                keys: _ => $"{prefix}name-dotnet-pluginconfig.json",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(1+n*2)
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        await using Stream s = await sut.FetchCodeZipAsync("name", CancellationToken.None);
        using var sr = new StreamReader(s, Encoding.UTF8);

        var result = await sr.ReadToEndAsync();

        result.Should().Be("latestVersionWithConfig");
        _backend.VerifyAll();
    }

    [Fact]
    public async Task ThrowExceptionIfNoConfigPresent()
    {
        string prefix = $"{GetType().Name}_{nameof(ThrowExceptionIfNoConfigPresent)}/";
        var versions = _backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet.zip"
            ).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var act = () => sut.FetchCodeZipAsync("name", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task ThrowExceptionIfOnlyOlderConfigPresent()
    {
        string prefix = $"{GetType().Name}_{nameof(FetchCodeStreamFromCorrectKey)}/";
        var versions = _backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet.zip",
                lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1)
            )
            .Concat(_backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet-pluginconfig.json",
                lastModifiedOffset: _ => TimeSpan.Zero
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var act = () => sut.FetchCodeZipAsync("name", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task ThrowExceptionIfDeleted()
    {
        string prefix = $"{GetType().Name}_{nameof(ThrowExceptionIfDeleted)}/";
        var versions = _backend.GenerateVersions(
                0, 2,
                keys: _ => $"{prefix}name-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n),
                isDeleted: n => n == 1
            )
            .Concat(_backend.GenerateVersions(
                1, 1,
                keys: _ => $"{prefix}name-dotnet-pluginconfig.json",
                lastModifiedOffset: _ => TimeSpan.FromMilliseconds(5)
            )).ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var act = () => sut.FetchCodeZipAsync("name", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _backend.VerifyAll();
    }

}