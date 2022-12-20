using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.Test.ExternalDependency;
using RKamphorst.PluginLoading.Test.PluginContract;
using Xunit;

namespace RKamphorst.PluginLoading.Test;

public class PluginLibraryFactoryShould
{
    private readonly Mock<IPluginLibrarySource> _librarySourceMock;
    private readonly PluginLibraryFactory _sut;

    public PluginLibraryFactoryShould()
    {
        _librarySourceMock = new Mock<IPluginLibrarySource>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock
            .Setup(m => m.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());

        _sut = new PluginLibraryFactory(
            Mock.Of<IPluginAssemblyLoaderFactory>(),
            _librarySourceMock.Object, loggerFactoryMock.Object
        );
    }
   
    [Fact]
    public async Task CreateLibraries()
    {
        _librarySourceMock
            .Setup(m => m.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b", "c" }.Select(n =>
                new PluginLibraryReference
                {
                    Name = n,
                    Source = _librarySourceMock.Object
                }
            ));
        
        _sut.AddServiceTypes(typeof(IService1), typeof(IService2));
        _sut.AddSharedTypes(typeof(IExternalInterface));

        var libs = await _sut.GetLibrariesAsync(CancellationToken.None);

        libs.Length.Should().Be(3);
        libs.Select(l => l.Reference.Name).Should().BeEquivalentTo("a", "b", "c");
        foreach (IPluginLibrary lib in libs)
        {
            lib.Reference.Source.Should().Be(_librarySourceMock.Object);
            lib.ServiceTypes.Should().BeEquivalentTo(new[] { typeof(IService1), typeof(IService2) });
            lib.SharedTypes.Should().BeEquivalentTo(new[] { typeof(IExternalInterface) });
        }
        _librarySourceMock.Verify(
            m => m.GetListAsync(It.IsAny<CancellationToken>()), 
            Times.Once
        );
    }

    [Fact]
    public async Task CreateLibrariesOnlyOnce()
    {
        _librarySourceMock
            .Setup(m => m.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b", "c" }.Select(n =>
                new PluginLibraryReference
                {
                    Name = n,
                    Source = _librarySourceMock.Object
                }
            ));
        
        _sut.AddServiceTypes(typeof(IService1), typeof(IService2));
        _sut.AddSharedTypes(typeof(IExternalInterface));

        await _sut.GetLibrariesAsync(CancellationToken.None);
        _librarySourceMock.Invocations.Clear();

        var libs = await _sut.GetLibrariesAsync(CancellationToken.None);
        libs.Length.Should().Be(3);
        
        _librarySourceMock.Verify(
            m => m.GetListAsync(It.IsAny<CancellationToken>()), 
            Times.Never
            );
    }
    
    [Fact]
    public async Task CreateLibrariesAgainIfOperationCanceled()
    {
        _librarySourceMock
            .SetupSequence(m => m.GetListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException())
            .ThrowsAsync(new OperationCanceledException())
            .ReturnsAsync(new[] { "a", "b", "c" }.Select(n =>
                new PluginLibraryReference
                {
                    Name = n,
                    Source = _librarySourceMock.Object
                }
            ))
            .ThrowsAsync(new OperationCanceledException());
        
        _sut.AddServiceTypes(typeof(IService1), typeof(IService2));
        _sut.AddSharedTypes(typeof(IExternalInterface));

        // first two times should fail
        var act = () => _sut.GetLibrariesAsync(CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();
        await act.Should().ThrowAsync<OperationCanceledException>();

        // third time should not fail
        await _sut.GetLibrariesAsync(CancellationToken.None);
        
        // every subsequent time should not invoke GetListAsync,
        // so should not fail
        var libs = await _sut.GetLibrariesAsync(CancellationToken.None);
        libs.Length.Should().Be(3);
        
        _librarySourceMock.Verify(
            m => m.GetListAsync(It.IsAny<CancellationToken>()), 
            Times.Exactly(3)
        );
    }
    
    [Fact]
    public async Task RefuseToAddServiceTypesAfterCreation()
    {
        _librarySourceMock
            .Setup(m => m.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b", "c" }.Select(n =>
                new PluginLibraryReference
                {
                    Name = n,
                    Source = _librarySourceMock.Object
                }
            ));
        
        _sut.AddServiceTypes(typeof(IService1), typeof(IService2));
        _sut.AddSharedTypes(typeof(IExternalInterface));

        await _sut.GetLibrariesAsync(CancellationToken.None);

        var act = () => _sut.AddServiceTypes(typeof(IService3));

        act.Should().Throw<InvalidOperationException>();
    }
    
    [Fact]
    public async Task RefuseToAddSharedTypesAfterCreation()
    {
        _librarySourceMock
            .Setup(m => m.GetListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b", "c" }.Select(n =>
                new PluginLibraryReference
                {
                    Name = n,
                    Source = _librarySourceMock.Object
                }
            ));
        
        _sut.AddServiceTypes(typeof(IService1), typeof(IService2));
        _sut.AddSharedTypes(typeof(IExternalInterface));

        await _sut.GetLibrariesAsync(CancellationToken.None);

        var act = () => _sut.AddSharedTypes(typeof(IService3));

        act.Should().Throw<InvalidOperationException>();
    }
}