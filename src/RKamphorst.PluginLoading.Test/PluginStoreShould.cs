using FluentAssertions;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.Test.Stubs;
using Xunit;

namespace RKamphorst.PluginLoading.Test;

public class PluginStoreShould
{
    private readonly Mock<StubPluginLibrarySource> _pluginLibrarySourceMock;
    private readonly PluginStore _sut;

    public PluginStoreShould()
    {
        _pluginLibrarySourceMock = StubPluginLibrarySource.CreateMock("Source");
        _sut = new PluginStore(Mock.Of<ILogger<PluginStore>>());
    }
    
    [Fact]
    public async Task DownloadCodeAndConfigAndReturnPrimaryAssembly() {
        var libraryReference = new PluginLibraryReference
        {
            Name = StubPluginLibraries.PluginA,
            Source = _pluginLibrarySourceMock.Object
        };

        var result = await _sut.GetPathToLibraryAssemblyAsync(libraryReference, CancellationToken.None);

        result.Should().NotBeEmpty();
        File.Exists(result).Should().BeTrue();

        _pluginLibrarySourceMock.Verify(
            m => m.FetchCodeZipAsync(StubPluginLibraries.PluginA, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _pluginLibrarySourceMock.Verify(
            m => m.FetchConfigAsync(StubPluginLibraries.PluginA, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
    
    [Fact]
    public async Task DownloadOnlyOnce()
    {
        var libraryReference = new PluginLibraryReference
        {
            Name = StubPluginLibraries.PluginA,
            Source = _pluginLibrarySourceMock.Object
        };

        await _sut.GetPathToLibraryAssemblyAsync(libraryReference, CancellationToken.None);

        _pluginLibrarySourceMock.Invocations.Clear();
        
        var result = await _sut.GetPathToLibraryAssemblyAsync(libraryReference, CancellationToken.None);
        result.Should().NotBeEmpty();
        File.Exists(result).Should().BeTrue();

        _pluginLibrarySourceMock.Verify(
            m => m.FetchCodeZipAsync(StubPluginLibraries.PluginA, It.IsAny<CancellationToken>()),
            Times.Never
        );
        _pluginLibrarySourceMock.Verify(
            m => m.FetchConfigAsync(StubPluginLibraries.PluginA, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ReturnValidAssembly()
    {
        var libraryReference = new PluginLibraryReference
        {
            Name = StubPluginLibraries.PluginA,
            Source = _pluginLibrarySourceMock.Object
        };

        var result = await _sut.GetPathToLibraryAssemblyAsync(libraryReference, CancellationToken.None);

        var assembly =
            PluginLoader
                .CreateFromAssemblyFile(result, cfg => cfg.PreferSharedTypes = true)
                .LoadDefaultAssembly();

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().Be(StubPluginLibraries.PluginA);
    }
}