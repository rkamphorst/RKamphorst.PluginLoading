using FluentAssertions;
using Moq;
using RKamphorst.PluginLoading.Contract;
using RKamphorst.PluginLoading.Test.ExternalDependency;
using RKamphorst.PluginLoading.Test.PluginContract;
using RKamphorst.PluginLoading.Test.Stubs;
using Xunit;

namespace RKamphorst.PluginLoading.Test;

public class PluginLibraryShould
{
    static PluginLibrary CreateSut(Type[] serviceTypes)
    {
        var source = new StubPluginLibrarySource("LibrarySource");
        source.AddLocalLibrary(StubPluginLibraries.PluginA);

        var reference = new PluginLibraryReference
        {
            Name = StubPluginLibraries.PluginA,
            Source = source
        };

        return new PluginLibrary(
            StubPluginLibraries.CreateAssemblyLoaderFactory(),
            reference, serviceTypes, Type.EmptyTypes
        );
    }
    
    [Fact]
    public void SetReferenceAndTypesWhenConstructed()
    {
        var reference = new PluginLibraryReference
        {
            Name = "Name",
            Source = Mock.Of<IPluginLibrarySource>()
        };
        var serviceTypes = new[] { typeof(IService1), typeof(IService2) };
        var sharedTypes = new[] { typeof(IExternalInterface) };

        var sut = new PluginLibrary(
            Mock.Of<IPluginAssemblyLoaderFactory>(), 
            reference, serviceTypes, sharedTypes
        );

        sut.Should().NotBeNull();
        sut.Reference.Should().BeEquivalentTo(reference);
        sut.ServiceTypes.Should().BeEquivalentTo(serviceTypes);
        sut.SharedTypes.Should().BeEquivalentTo(sharedTypes);
    }
    
    [Fact]
    public async Task GetPluginsForClosedInterface()
    {
        var sut = CreateSut(new[] { typeof(IService1) });

        var plugins = (await sut.GetPluginsAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();
        
        foreach (var plugin in plugins)
        {
            plugin.Implementation.Should().BeAssignableTo(typeof(IService1));
            plugin.Services.Should().BeEquivalentTo(new[] { typeof(IService1) });
        }
    }
    
    [Fact]
    public async Task GetPluginsForClosedBaseClass()
    {
        var sut = CreateSut(new[] { typeof(BaseClass4) });

        var plugins = (await sut.GetPluginsAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();
        
        foreach (var plugin in plugins)
        {
            plugin.Implementation.Should().BeAssignableTo(typeof(BaseClass4));
            plugin.Services.Should().BeEquivalentTo(new[] { typeof(BaseClass4) });
        }
    }
    
    [Fact]
    public async Task GetPluginsForOpenGenericInterface()
    {
        var sut = CreateSut(new[] { typeof(IGenericService6<,,>) });

        var plugins = (await sut.GetPluginsAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();
        foreach (var plugin in plugins)
        {
            foreach (var service in plugin.Services)
            {
                service.GetGenericTypeDefinition().Should().Be(typeof(IGenericService6<,,>));
                plugin.Implementation.Should().BeAssignableTo(service);
            }
        }
    }
    
    [Fact]
    public async Task GetPluginsForOpenGenericBaseClass()
    {
        var sut = CreateSut(new[] { typeof(GenericBaseClass5<,>) });

        var plugins = (await sut.GetPluginsAsync(CancellationToken.None)).ToArray();

        plugins.Should().NotBeEmpty();
        foreach (var plugin in plugins)
        {
            foreach (var service in plugin.Services)
            {
                service.GetGenericTypeDefinition().Should().Be(typeof(GenericBaseClass5<,>));
                plugin.Implementation.Should().BeAssignableTo(service);
            }
        }
    }
}