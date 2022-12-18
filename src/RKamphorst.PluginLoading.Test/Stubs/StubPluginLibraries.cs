using McMaster.NETCore.Plugins;
using Moq;

namespace RKamphorst.PluginLoading.Test.Stubs;

public static class StubPluginLibraries
{
    public static string PluginA = "RKamphorst.PluginLoading.Test.PluginA";
    public static string PluginB = "RKamphorst.PluginLoading.Test.PluginB";
    
    public static string GetPathToAssemblyFolder(string name)
    {
        var thisType = typeof(StubPluginLibraries);
        string assemblyName = thisType.Assembly.GetName().Name!;
        string assemblyPath = Path.GetDirectoryName(thisType.Assembly.Location)!;

        var segments = new List<string>();
        do
        {
            segments.Add(Path.GetFileName(assemblyPath));
            assemblyPath = Path.GetDirectoryName(assemblyPath)!;
        } while (segments[^1] != assemblyName);

        segments.Reverse();
        segments.RemoveAt(0);

        assemblyPath = Path.Combine(
            new[] { assemblyPath }
                .Concat(new[] { name })
                .Concat(segments)
                .ToArray()
        );
        return assemblyPath;
    }

    public static string GetPathToAssemblyFile(string name)
    {
        return Path.Combine(GetPathToAssemblyFolder(name), $"{name}.dll");
    }

    public static IPluginAssemblyLoaderFactory CreateAssemblyLoaderFactory()
    {
        var assemblyLoaderFactoryMock = new Mock<IPluginAssemblyLoaderFactory>();
        assemblyLoaderFactoryMock
            .Setup(
                m => m.Create(It.IsAny<IPluginLibrary>())
            ).Returns((IPluginLibrary lib) =>
            {
                var assemblyLoaderMock = new Mock<IPluginAssemblyLoader>();
                assemblyLoaderMock
                    .Setup(m => m.GetOrLoadAssemblyAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CancellationToken _) =>
                        PluginLoader
                            .CreateFromAssemblyFile(
                                GetPathToAssemblyFile(lib.Reference.Name),
                                false,
                                lib.SharedTypes.Concat(lib.ServiceTypes).ToArray()
                            )
                            .LoadDefaultAssembly());
                return assemblyLoaderMock.Object;
            });
        return assemblyLoaderFactoryMock.Object;
    }
}