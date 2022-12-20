using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.Test.ExternalDependency;
using RKamphorst.PluginLoading.Test.PluginContract;
using RKamphorst.PluginLoading.Test.Stubs;
using Xunit;

namespace RKamphorst.PluginLoading.Test;

public class PluginBuilderShould
{
    private readonly PluginBuilder _sut;

    public PluginBuilderShould()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock
            .Setup(m => m.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
        _sut = new PluginBuilder(StubPluginLibraries.CreateAssemblyLoaderFactory(), loggerFactoryMock.Object);
    }


    [Fact]
    public async Task AddPluginLibrarySource()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);
        
        _sut.AddPluginsFromSource<IService1>( librarySourceMock.Object);
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();
        foreach (var plugin in plugins)
        {
            plugin.Implementation.Should().BeAssignableTo<IService1>();
            plugin.Services.Should().Contain(typeof(IService1));
        }
    }
    
    [Fact]
    public async Task AddPluginLibrarySourceWithMultipleServices()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);
        
        _sut.AddPluginsFromSource<IService1>( librarySourceMock.Object);
        _sut.AddPluginsFromSource<IService2>( librarySourceMock.Object);
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();

        var plugins1 = plugins
            .Where(p => typeof(IService1).IsAssignableFrom(p.Implementation)).ToArray();
        plugins1.Should().NotBeEmpty();
        foreach (var plugin in plugins1)
        {
            plugin.Services.Should().BeEquivalentTo(new[] { typeof(IService1) });
        }

        var plugins2 = plugins
            .Where(p => typeof(IService2).IsAssignableFrom(p.Implementation)).ToArray();
        plugins2.Should().NotBeEmpty();
        foreach (var plugin in plugins2)
        {
            plugin.Services.Should().Contain(typeof(IService2));
        }

        plugins.Should().BeEquivalentTo(plugins1.Concat(plugins2).Distinct());
    }
    
    [Fact]
    public async Task AddPluginLibrarySourceWithMultipleServicesFromDifferentLibs()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);
        
        _sut.AddPluginsFromSource<IService3>( librarySourceMock.Object);
        _sut.AddPluginsFromSource<IExternalInterface>( librarySourceMock.Object);
        
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();

        var plugins1 = plugins
            .Where(p => typeof(IService3).IsAssignableFrom(p.Implementation)).ToArray();
        plugins1.Should().NotBeEmpty();
        foreach (var plugin in plugins1)
        {
            plugin.Services.Should().Contain(typeof(IService3));
        }

        var plugins2 = plugins
            .Where(p => typeof(IExternalInterface).IsAssignableFrom(p.Implementation)).ToArray();
        plugins2.Should().NotBeEmpty();
        foreach (var plugin in plugins2)
        {
            plugin.Services.Should().Contain(typeof(IExternalInterface));
        }
        
        plugins.Should().BeEquivalentTo(plugins1.Concat(plugins2).Distinct());
    }

    [Fact]
    public async Task NotShareTypeIfNotShared()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);

        _sut.AddPluginsFromSource<IService3>(librarySourceMock.Object);
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        var pluginsImplementingShared =
            plugins
                .Where(p => p.Implementation.IsAssignableTo(typeof(IExternalInterface)))
                .ToArray();
        pluginsImplementingShared.Should().BeEmpty();
    }

    [Fact]
    public async Task ShareTypeIfSharedBeforeAdd()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);

        _sut.ShareWithPlugins<IExternalInterface>();
        _sut.AddPluginsFromSource<IService3>( librarySourceMock.Object );
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        var pluginsImplementingShared =
            plugins
                .Where(p => p.Implementation.IsAssignableTo(typeof(IExternalInterface)))
                .ToArray();
            
        pluginsImplementingShared.Should().NotBeEmpty();
        foreach (var plugin in pluginsImplementingShared)
        {
            plugin.Implementation.Should().BeAssignableTo<IService3>();
            plugin.Implementation.Should().BeAssignableTo<IExternalInterface>();
            plugin.Services.Should().Contain(typeof(IService3));
            
            // since IExternalInterface was not added as a service, it should not be reported as such
            plugin.Services.Should().NotContain(typeof(IExternalInterface));
        }
    }
    
    [Fact]
    public async Task ShareTypeIfSharedAfterAdd()
    {
        var librarySourceMock = StubPluginLibrarySource.CreateMock("LibrarySource");
        librarySourceMock.Object.AddLocalLibrary(StubPluginLibraries.PluginA);

        _sut.ShareWithPlugins<IExternalInterface>();
        _sut.AddPluginsFromSource<IService3>( librarySourceMock.Object );
        var plugins = (await _sut.BuildAsync(CancellationToken.None)).ToArray();

        var pluginsImplementingShared =
            plugins
                .Where(p => p.Implementation.IsAssignableTo(typeof(IExternalInterface)))
                .ToArray();
            
        pluginsImplementingShared.Should().NotBeEmpty();
        foreach (var plugin in pluginsImplementingShared)
        {
            plugin.Implementation.Should().BeAssignableTo<IService3>();
            plugin.Implementation.Should().BeAssignableTo<IExternalInterface>();
            plugin.Services.Should().Contain(typeof(IService3));
            
            // since IExternalInterface was not added as a service, it should not be reported as such
            plugin.Services.Should().NotContain(typeof(IExternalInterface));
        }
    }
}