using Geopilot.PipelineCore.Pipeline;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private readonly string pluginPath;

    private ProcessPluginLoadContext(string pluginPath)
        : base(name: pluginPath, isCollectible: true)
    {
        this.pluginPath = pluginPath;
        resolver = new AssemblyDependencyResolver(pluginPath);
        hostPiplineCoreAssembly = Default.Assemblies.First(a => a.GetName().Name == PipelineCoreAssemblyName);
    }

    /// <summary>
    /// Creates a <see cref="ProcessPluginLoadContext"/> for the plugin at <paramref name="pluginPath"/>,
    /// but only if the plugin's referenced <c>Geopilot.PipelineCore</c> is compatible with the host's.
    /// Returns <see langword="null"/> when the plugin is rejected; the caller should skip the plugin in
    /// that case. Compatibility is checked by reading the plugin's manifest only — no plugin code is
    /// executed. This matters because <see cref="AssemblyLoadContext.LoadFromAssemblyPath(string)"/>
    /// would make module initializers and (on first type access) type constructors runnable before
    /// the host could decide whether to reject the assembly.
    /// </summary>
    /// <param name="pluginPath">Absolute path to the plugin assembly file.</param>
    /// <param name="logger">Logger used to report rejection reasons and version warnings.</param>
    /// <returns>A new context if the plugin is compatible; otherwise <see langword="null"/>.</returns>
    public static ProcessPluginLoadContext? Create(string pluginPath, ILogger logger)
    {
        if (!ValidateCompatibility(pluginPath, logger))
            return null;

        return new ProcessPluginLoadContext(pluginPath);
    }

    /// <summary>
    /// Verifies that the plugin assembly references a compatible version of <c>Geopilot.PipelineCore</c>.
    /// Rejects plugins whose referenced major version differs from the host's loaded major version or use a higher minor version than the host,
    /// and warns when the plugin was built against an older minor/patch.
    /// </summary>
    private static bool ValidateCompatibility(string pluginPath, ILogger logger)
    {
        var coreVersionUsedByHost = typeof(IPipelineFile).Assembly.GetName().Version;
        string pluginDisplayName = Path.GetFileNameWithoutExtension(pluginPath);

        // MetadataLoadContext needs a corelib in its resolver paths to anchor the type system,
        // so we feed it every assembly from the shared framework directory plus the plugin
        // itself. GetReferencedAssemblies() then reads the plugin's manifest table directly —
        // no plugin code is executed, and PipelineCore does not need to be physically resolvable
        // next to the plugin (the recorded AssemblyRef carries the version we want).
        var resolverPaths = new List<string>(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        {
            pluginPath,
        };
        using var metadataContext = new MetadataLoadContext(new PathAssemblyResolver(resolverPaths));
        var pluginAssembly = metadataContext.LoadFromAssemblyPath(pluginPath);

        var coreReference = pluginAssembly.GetReferencedAssemblies()
            .FirstOrDefault(r => r.Name == PipelineCoreAssemblyName);

        if (coreReference == null)
        {
            logger.LogError(
                "Plugin '{Plugin}' does not reference {Core}; rejecting.",
                pluginDisplayName,
                PipelineCoreAssemblyName);
            return false;
        }

        var coreVersionUsedByPlugin = coreReference.Version;

        if (coreVersionUsedByPlugin == null || coreVersionUsedByHost == null)
        {
            logger.LogError(
                "Unable to determine {Core} version for plugin '{Plugin}' (plugin={PluginVersion}, host={HostVersion}); rejecting.",
                PipelineCoreAssemblyName,
                pluginDisplayName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
            return false;
        }

        if (coreVersionUsedByPlugin.Major != coreVersionUsedByHost.Major)
        {
            logger.LogError(
                "Plugin '{Plugin}' was built against {Core} {PluginVersion} but host runs {HostVersion}; major versions differ, plugin will not be loaded.",
                pluginDisplayName,
                PipelineCoreAssemblyName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
            return false;
        }

        if (coreVersionUsedByPlugin.Minor > coreVersionUsedByHost.Minor)
        {
            logger.LogError(
                "Plugin '{Plugin}' was built against {Core} {PluginVersion} but host runs {HostVersion}; minor version of plugin is higher than minor version of host, plugin will not be loaded.",
                pluginDisplayName,
                PipelineCoreAssemblyName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
            return false;
        }

        if (coreVersionUsedByPlugin < coreVersionUsedByHost)
        {
            logger.LogWarning(
                "Plugin '{Plugin}' was built against older {Core} {PluginVersion} (host runs {HostVersion}); consider rebuilding the plugin.",
                pluginDisplayName,
                PipelineCoreAssemblyName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
        }

        return true;
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
