using FluentAssertions;
using Xunit;

namespace RKamphorst.PluginLoading.Aws.Test.S3LibrarySource;

public class GetListAsyncShould
{
    private readonly IS3TestBackend _backend;

    public GetListAsyncShould()
    {
        _backend = S3TestBackendExtensions.CreateBackend();
    }

    
    [Fact]
    public async Task ImplementContinuation()
    {
        string prefix = $"{GetType().Name}_{nameof(ImplementContinuation)}/";
        var versions = 
            _backend.GenerateVersions(0, 1000, 
                    keys: n => $"{prefix}lib{n}-dotnet.zip",
                    lastModifiedOffset: _ => TimeSpan.Zero
                    )
            .Concat(
                _backend.GenerateVersions(0, 1000, 
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1))
            )
            .Shuffle().ToList();

        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, 1000).Select(n => $"lib{n}").ToList()
            );
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task OnlyListLibrariesThatHaveConfigAndCode()
    {
        string prefix = $"{GetType().Name}_{nameof(OnlyListLibrariesThatHaveConfigAndCode)}/";
        var versions = 
            _backend.GenerateVersions(0, 10, 
                    keys: n => $"{prefix}lib{n}-dotnet.zip",
                    lastModifiedOffset: _ => TimeSpan.Zero)
            .Concat(
                _backend.GenerateVersions(5, 10, 
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1))
            )
            .Shuffle().ToList();
        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(5, 5).Select(n => $"lib{n}").ToList()
        );
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task OnlyListLibrariesThatHaveConfigYoungerThanCode()
    {
        string prefix = $"{GetType().Name}_{nameof(OnlyListLibrariesThatHaveConfigYoungerThanCode)}/";
        var versions = _backend.GenerateVersions(0, 10,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds((n + 6) * 200)
            )
            .Concat(
                _backend.GenerateVersions(0, 10,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: n => TimeSpan.FromMilliseconds(n * 2 * 200)
                )
            )
            .Shuffle().ToList();
        
        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(6, 4).Select(n => $"lib{n}").ToList()
        );
        _backend.VerifyAll();
    }

    [Fact]
    public async Task OnlyListLibrariesBeforeVersionAtDate()
    {
        string prefix = $"{GetType().Name}_{nameof(OnlyListLibrariesBeforeVersionAtDate)}/";
        var versions = _backend.GenerateVersions(0, 10,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.FromMilliseconds(n-5)
            )
            .Concat(
                _backend.GenerateVersions(0, 10,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: n => TimeSpan.FromMilliseconds(n-4)
                )
            )
            .Shuffle().ToList();
        
        var referenceDateTime = await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix, referenceDateTime);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, 5).Select(n => $"lib{n}").ToList()
        );
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task OnlyListLibrariesWithCodeThatIsNotDeleted()
    {
        string prefix = $"{GetType().Name}_{nameof(OnlyListLibrariesWithCodeThatIsNotDeleted)}/";
        var versions = _backend.GenerateVersions(0, 2,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.Zero
            )
            .Concat(
                _backend.GenerateVersions(0, 2,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1)
                )
            )
            .Concat(
                _backend.GenerateVersions(1, 1,
                    keys: n => $"{prefix}lib{n}-dotnet.zip",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(2),
                    isDeleted: _ => true
                )
            )
            .Shuffle().ToList();
        
        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, 1).Select(n => $"lib{n}").ToList()
        );
        _backend.VerifyAll();
    }
    
    [Fact]
    public async Task OnlyListLibrariesWithConfigThatIsNotDeleted()
    {
        string prefix = $"{GetType().Name}_{nameof(OnlyListLibrariesWithConfigThatIsNotDeleted)}/";
        var versions = _backend.GenerateVersions(0, 2,
                keys: n => $"{prefix}lib{n}-dotnet.zip",
                lastModifiedOffset: n => TimeSpan.Zero
            )
            .Concat(
                _backend.GenerateVersions(0, 2,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(1)
                )
            )
            .Concat(
                _backend.GenerateVersions(1, 1,
                    keys: n => $"{prefix}lib{n}-dotnet-pluginconfig.json",
                    lastModifiedOffset: _ => TimeSpan.FromMilliseconds(2),
                    isDeleted: _ => true
                )
            )
            .Shuffle().ToList();
        
        await _backend.Setup(prefix, _backend.ReferenceDateTime(), versions);

        var sut = _backend.CreateSystemUnderTest(prefix);

        var result = (await sut.GetListAsync(CancellationToken.None)).ToArray();
        result.Should().NotBeNull();
        result.Select(l => l.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, 1).Select(n => $"lib{n}").ToList()
        );
        _backend.VerifyAll();
    }
}