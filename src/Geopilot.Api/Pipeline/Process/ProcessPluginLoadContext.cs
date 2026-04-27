using System.Reflection;
using System.Runtime.Loader;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Collectible <see cref="AssemblyLoadContext"/> for processor plugins. Follows the
/// Microsoft plugin pattern: private dependencies are resolved through the plugin's
/// <c>.deps.json</c> via <see cref="AssemblyDependencyResolver"/>, while assemblies
/// shared with the host are returned from the default ALC so plugin and host see the
/// same <see cref="Type"/> identity for contract types like
/// <see cref="Geopilot.PipelineCore.Pipeline.IPipelineFile"/> and
/// <see cref="Microsoft.Extensions.Logging.ILogger"/>.
/// </summary>
internal sealed class ProcessPluginLoadContext : AssemblyLoadContext
{
    private const string PipelineCoreAssemblyName = "Geopilot.PipelineCore";
    private static readonly HashSet<string> HostSharedAssemblies = new(StringComparer.Ordinal)
    {
        "Microsoft.Extensions.Logging.Abstractions",
    };

    private readonly AssemblyDependencyResolver resolver;
    private readonly Assembly hostPiplineCoreAssembly;

    public ProcessPluginLoadContext(string pluginPath)
        : base(name: pluginPath, isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginPath);
        hostPiplineCoreAssembly = Default.Assemblies.First(a => a.GetName().Name == PipelineCoreAssemblyName);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // If the assembly to load is PipelineCore, return the hosts assembly directly.
        // Returning null here would cause the default ALC to load the assembly.
        // That would be a problem because the default ALC only resolves a requested assembly,
        // if the found assembly has a version <= the requested version.
        // In our case that might be the case, because the default ALC uses the PipelineCore DLL vom the referenced project.
        // The version in the referenced project does not specify a patch number, but the NuGet package does.
        // So the NuGet package version, which is the requested version, is higher than the host/default version, because it has the patch number at the end.
        if (assemblyName.Name != null && assemblyName.Name == PipelineCoreAssemblyName)
            return hostPiplineCoreAssembly;

        // If the assembly to load is a dependency that is shared with the host, return null.
        // This resolves the plugins' reference to the same assembly as the hosts'.
        if (assemblyName.Name != null && HostSharedAssemblies.Contains(assemblyName.Name))
            return null;

        var path = resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path != null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
