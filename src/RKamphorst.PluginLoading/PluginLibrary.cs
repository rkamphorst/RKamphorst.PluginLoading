using System.Reflection;
using RKamphorst.PluginLoading.Contract;

namespace RKamphorst.PluginLoading;

public class PluginLibrary : IPluginLibrary
{
    private readonly IPluginAssemblyLoader _assemblyLoader;

    public PluginLibrary(
        IPluginAssemblyLoaderFactory assemblyLoaderFactory, 
        PluginLibraryReference libraryReference, 
        IEnumerable<Type> serviceTypes,
        IEnumerable<Type> sharedTypes
        )
    {
        Reference = libraryReference;
        ServiceTypes = serviceTypes.ToArray();
        SharedTypes = sharedTypes.ToArray();
        _assemblyLoader = assemblyLoaderFactory.Create(this);
    }

    public PluginLibraryReference Reference { get; }

    public IReadOnlyCollection<Type> ServiceTypes { get; }
    
    public IReadOnlyCollection<Type> SharedTypes { get; }

    public async Task<IEnumerable<Plugin>> GetPluginsAsync(CancellationToken cancellationToken)
    {
        var assembly = await _assemblyLoader.GetOrLoadAssemblyAsync(cancellationToken);
        return GetPlugins(assembly);
    }

    IEnumerable<Plugin> GetPlugins(Assembly assembly)
    {
        return assembly.GetTypes()
            .SelectMany(_ => ServiceTypes,
                (assemblyType, serviceType) => new
                {
                    Implementation = assemblyType,
                    Services = GetMatchingServices(assemblyType, serviceType)
                })
            .GroupBy(implServicesPair => implServicesPair.Implementation)
            .Select(pluginGroup => new Plugin
            {
                Implementation = pluginGroup.Key, Services = pluginGroup.SelectMany(x => x.Services).ToArray()
            })
            .Where(plugin => plugin.Services.Any());
    }

    private IEnumerable<Type> GetMatchingServices(Type toMatch, Type serviceType)
    {
        if (toMatch.IsInterface || toMatch.IsAbstract || !toMatch.IsPublic)
        {
            // only concrete and public types can really provide services,
            // so in this case we return nothing
            yield break;
        }

        if (!serviceType.ContainsGenericParameters)
        {
            // serviceType is a closed type, so we can simply
            // check whether toMatch is assignable to it
            if (serviceType.IsAssignableFrom(toMatch))
            {
                // if so, this service is provided by toMatch
                yield return serviceType;
            }

            yield break;
        }

        // serviceType is an open generic type.
        // we need to do some extra complicated things to find out which closed types
        // are services matching that open generic type are provided by toMatch
        foreach (Type svc in GetServicesMatchingOpenGenericServiceType(toMatch, serviceType))
        {
            yield return svc;
        }
    }

    private IEnumerable<Type> GetServicesMatchingOpenGenericServiceType(Type toMatch, Type openGenericServiceType)
    {
        // openGenericServiceType can be either a generic type definition,
        // or a partially constructed generic type.
        Type oSvcGenericTypeDef = openGenericServiceType.IsGenericTypeDefinition
            ? openGenericServiceType
            : openGenericServiceType.GetGenericTypeDefinition();

        IEnumerable<Type> baseTypesWithSameGenericType =
            EnumerateInterfacesAndBaseTypes(toMatch).Where(
                t => t.IsGenericType && t.GetGenericTypeDefinition() == oSvcGenericTypeDef
            );

        foreach (Type t in baseTypesWithSameGenericType)
        {
            Type[] typeArgs = openGenericServiceType.GetGenericArguments()
                .Zip(t.GetGenericArguments(), (a, b) => a.IsGenericTypeParameter || a == b ? b : null)
                .Where(ta => ta != null).Select(ta => ta!)
                .ToArray();

            if (typeArgs.Length == oSvcGenericTypeDef.GetGenericArguments().Length)
            {
                // TODO: what if type args are all type parameters
                yield return oSvcGenericTypeDef.MakeGenericType(typeArgs);
            }
        }
    }

    private static IEnumerable<Type> EnumerateInterfacesAndBaseTypes(Type t)
    {
        foreach (Type i in t.GetInterfaces())
        {
            yield return i;
        }

        Type? baseType = t;
        while (baseType != null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }

}