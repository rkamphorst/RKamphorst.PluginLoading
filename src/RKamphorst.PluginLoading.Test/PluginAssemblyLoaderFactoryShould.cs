using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.Test.PluginContract;
using RKamphorst.PluginLoading.Test.Stubs;
using Xunit;

namespace RKamphorst.PluginLoading.Test;

public class PluginAssemblyLoaderFactoryShould
{
    private readonly PluginAssemblyLoaderFactory _sut;
    private readonly Mock<IPluginLibrary> _pluginLibraryMock;
    private readonly Mock<IPluginStore> _pluginStoreMock;

    public PluginAssemblyLoaderFactoryShould()
    {
        _pluginStoreMock = new Mock<IPluginStore>();
        _pluginStoreMock
            .Setup(m => m.GetPathToLibraryAssemblyAsync(It.IsAny<PluginLibraryReference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(StubPluginLibraries.GetPathToAssemblyFile(StubPluginLibraries.PluginA));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock
            .Setup(m => m.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        _pluginLibraryMock = new Mock<IPluginLibrary>();
        _pluginLibraryMock
            .SetupGet(m => m.Reference)
            .Returns(new PluginLibraryReference { Name = StubPluginLibraries.PluginA });
        _pluginLibraryMock.SetupGet(m => m.ServiceTypes).Returns(new[] { typeof(IService1) });
        _pluginLibraryMock.SetupGet(m => m.SharedTypes).Returns(Type.EmptyTypes);
        
        _sut = new PluginAssemblyLoaderFactory(_pluginStoreMock.Object, loggerFactoryMock.Object);
    }
    
    [Fact]
    public void CreateAssemblyLoader()
    {
        IPluginAssemblyLoader pluginAssemblyLoader = _sut.Create(_pluginLibraryMock.Object);
        pluginAssemblyLoader.Should().NotBeNull();
    }
    
    [Fact]
    public async Task CreateAssemblyLoaderThatLoadsAssembly()
    {
        IPluginAssemblyLoader pluginAssemblyLoader = _sut.Create(_pluginLibraryMock.Object);

        Assembly assembly = await pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().Be(StubPluginLibraries.PluginA);
        _pluginStoreMock.Verify(
            m => m.GetPathToLibraryAssemblyAsync(It.IsAny<PluginLibraryReference>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task CreateAssemblyLoaderThatLoadsAssemblyOnlyOnce()
    {
        IPluginAssemblyLoader pluginAssemblyLoader = _sut.Create(_pluginLibraryMock.Object);
        await pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);
        _pluginStoreMock.Invocations.Clear();
        
        Assembly assembly = await pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().Be(StubPluginLibraries.PluginA);
        
        _pluginStoreMock.Verify(
            m => m.GetPathToLibraryAssemblyAsync(It.IsAny<PluginLibraryReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task CreateAssemblyLoaderThatLoadsAssemblyAgainIfOperationCanceled()
    {
        _pluginStoreMock.SetupSequence(
                m => m.GetPathToLibraryAssemblyAsync(It.IsAny<PluginLibraryReference>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new OperationCanceledException())
            .ThrowsAsync(new OperationCanceledException())
            .ReturnsAsync(StubPluginLibraries.GetPathToAssemblyFile(StubPluginLibraries.PluginA))
            .ThrowsAsync(new OperationCanceledException());
        
        IPluginAssemblyLoader pluginAssemblyLoader = _sut.Create(_pluginLibraryMock.Object);

        var act = () => pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);
        
        // the first two times, operation is canceled
        await act.Should().ThrowAsync<OperationCanceledException>();
        await act.Should().ThrowAsync<OperationCanceledException>();
        
        // third time, operation is not canceled
        await pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);

        // fourth time the result should be cached, so operation cannot be canceled
        Assembly assembly = await pluginAssemblyLoader.GetOrLoadAssemblyAsync(CancellationToken.None);

        assembly.Should().NotBeNull();
        assembly.GetName().Name.Should().Be(StubPluginLibraries.PluginA);
        
        _pluginStoreMock.Verify(
            m => m.GetPathToLibraryAssemblyAsync(It.IsAny<PluginLibraryReference>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
    
}