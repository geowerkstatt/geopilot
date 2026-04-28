using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Provides functionality to create and initialize pipeline process instances based on configuration settings and
/// registered plugins.
/// </summary>
/// <remarks>The PipelineProcessFactory is responsible for resolving and instantiating process implementations
/// specified in pipeline configuration. It supports both built-in and plugin-based process types, allowing for
/// extensibility via external assemblies. This factory is typically used in scenarios where pipeline steps are
/// dynamically configured and require runtime resolution of their processing logic. Thread safety is not guaranteed; if
/// used concurrently, external synchronization is required.</remarks>
public class PipelineProcessFactory : IPipelineProcessFactory, IDisposable
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<PipelineProcessFactory> logger;
    private readonly PipelineOptions pipelineOptions;

    private HashSet<Assembly> processorPluginAssemblies = new HashSet<Assembly>();
    private HashSet<AssemblyLoadContext> processorPluginLoadContexts = new HashSet<AssemblyLoadContext>();
    private bool disposed;

    /// <summary>
    /// Disposes the resources used by the PipelineProcessFactory.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources and unloads assemblies.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                foreach (var processorPluginLoadContext in processorPluginLoadContexts)
                {
                    processorPluginLoadContext.Unload();
                }

                processorPluginLoadContexts.Clear();
                processorPluginAssemblies.Clear();
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Initializes a new instance of the PipelineProcessFactory class using the specified configuration settings.
    /// </summary>
    /// <remarks>The constructor loads plugin assemblies specified in the "Pipeline:Plugins" section of the
    /// configuration. Assemblies are loaded into a dedicated context, allowing for isolation and dynamic plugin
    /// management. If no plugins are configured, the factory will operate without any loaded assemblies.</remarks>
    /// <param name="pipelinePluginOptions">Pipeline plugin options containing configuration settings. Cannot be null.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers for process instances. Cannot be null.</param>
    public PipelineProcessFactory(IOptions<PipelineOptions> pipelinePluginOptions, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(pipelinePluginOptions);

        this.loggerFactory = loggerFactory;
        this.pipelineOptions = pipelinePluginOptions.Value;
        var processorPlugins = pipelineOptions.Plugins;
        this.logger = loggerFactory.CreateLogger<PipelineProcessFactory>();

        if (processorPlugins != null)
        {
            foreach (var assemblyPath in processorPlugins)
            {
                var assemblyFullPath = Path.IsPathRooted(assemblyPath) ? assemblyPath : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, assemblyPath));

                // Validate compatibility against the plugin's metadata before loading it for
                // execution. LoadFromAssemblyPath would make the plugin's code runnable
                // (module initializers, type cctors triggered by subsequent reflection), so
                // incompatible or untrusted assemblies must be rejected first.
                if (!ValidatePluginCoreCompatibility(assemblyFullPath))
                {
                    continue;
                }

                var assemblyContext = new AssemblyLoadContext(assemblyFullPath, isCollectible: true);
                var plugin = assemblyContext.LoadFromAssemblyPath(assemblyFullPath);

                processorPluginAssemblies.Add(plugin);
                processorPluginLoadContexts.Add(assemblyContext);
            }
        }
    }

    /// <summary>
    /// Verifies that a plugin assembly references a compatible version of Geopilot.PipelineCore.
    /// The check reads the assembly's manifest via <see cref="PEReader"/> and
    /// <see cref="MetadataReader"/> — no code from the plugin is executed. This matters because
    /// <see cref="AssemblyLoadContext.LoadFromAssemblyPath(string)"/> would make module
    /// initializers and (on first type access) type constructors runnable before the host could
    /// decide whether to reject the assembly. The check rejects plugins whose referenced major
    /// version differs from the host's loaded major version, and warns when the plugin was built
    /// against an older minor/patch.
    /// </summary>
    /// <param name="assemblyPath">Absolute path to the plugin assembly file.</param>
    /// <returns>True if the plugin is compatible with the host's PipelineCore; false otherwise.</returns>
    private bool ValidatePluginCoreCompatibility(string assemblyPath)
    {
        const string coreAssemblyName = "Geopilot.PipelineCore";
        var coreVersionUsedByHost = typeof(IPipelineFile).Assembly.GetName().Version;

        string pluginDisplayName = Path.GetFileNameWithoutExtension(assemblyPath);
        Version? coreVersionUsedByPlugin = null;
        bool coreReferenceFound = false;

        var resolver = new AssemblyDependencyResolver(assemblyPath);
        var path = resolver.ResolveAssemblyToPath(new AssemblyName(coreAssemblyName));
        if (path != null)
        {
            coreReferenceFound = true;
            var name = AssemblyName.GetAssemblyName(path);
            coreVersionUsedByPlugin = AssemblyName.GetAssemblyName(path).Version;
        }
        else
        {
            return false;
        }

        if (!coreReferenceFound)
        {
            logger.LogError(
                "Plugin '{Plugin}' does not reference {Core}; rejecting.",
                pluginDisplayName,
                coreAssemblyName);
            return false;
        }

        if (coreVersionUsedByPlugin == null || coreVersionUsedByHost == null)
        {
            logger.LogError(
                "Unable to determine {Core} version for plugin '{Plugin}' (plugin={PluginVersion}, host={HostVersion}); rejecting.",
                coreAssemblyName,
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
                coreAssemblyName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
            return false;
        }

        if (coreVersionUsedByPlugin < coreVersionUsedByHost)
        {
            logger.LogWarning(
                "Plugin '{Plugin}' was built against older {Core} {PluginVersion} (host runs {HostVersion}); consider rebuilding the plugin.",
                pluginDisplayName,
                coreAssemblyName,
                coreVersionUsedByPlugin,
                coreVersionUsedByHost);
        }

        return true;
    }

    /// <inheritdoc />
    public IPipelineProcessBuilder Builder()
    {
        return new PipelineProcessBuilder(processorPluginAssemblies, loggerFactory, pipelineOptions);
    }

    internal class PipelineProcessBuilder : IPipelineProcessBuilder
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        private readonly HashSet<Assembly> processorPluginAssemblies = new HashSet<Assembly>();
        private readonly PipelineOptions pipelineOptions;

        private string? pipelineId;
        private StepConfig? stepConfig;
        private List<ProcessConfig>? processes;
        private string? pipelineDirectory;
        private Guid jobId;

        /// <summary>
        /// Initializes a new instance of the PipelineProcessBuilder class with the specified plugin assemblies, load
        /// contexts, logger factory, and pipeline options.
        /// </summary>
        /// <remarks>Use this constructor to configure the PipelineProcessBuilder with all required
        /// dependencies for plugin-based pipeline processing. Supplying appropriate assemblies and load contexts allows
        /// for flexible plugin management and versioning.</remarks>
        /// <param name="processorPluginAssemblies">A set of assemblies that contain processor plugins to be included in the pipeline.</param>
        /// <param name="loggerFactory">The factory used to create loggers for pipeline processing operations.</param>
        /// <param name="pipelineOptions">The options that configure the behavior and execution parameters of the pipeline.</param>
        public PipelineProcessBuilder(
            HashSet<Assembly> processorPluginAssemblies,
            ILoggerFactory loggerFactory,
            PipelineOptions pipelineOptions)
        {
            this.processorPluginAssemblies = processorPluginAssemblies;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<PipelineProcessBuilder>();
            this.pipelineOptions = pipelineOptions;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder PipelineId(string pipelineId)
        {
            this.pipelineId = pipelineId;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder StepConfig(StepConfig stepConfig)
        {
            this.stepConfig = stepConfig;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder Processes(List<ProcessConfig> processes)
        {
            this.processes = processes;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder PipelineDirectory(string pipelineDirectory)
        {
            this.pipelineDirectory = pipelineDirectory;
            return this;
        }

        /// <inheritdoc />
        public IPipelineProcessBuilder JobId(Guid jobId)
        {
            this.jobId = jobId;
            return this;
        }

        /// <inheritdoc />
        public object Build()
        {
            ArgumentNullException.ThrowIfNull(pipelineDirectory);

            var (objectType, constructor, processParameterization) = PrepareProcessDescriptor();
            var parameters = constructor.GetParameters()
                .Select(p => GenerateParameter(p, objectType, processParameterization, pipelineDirectory, jobId))
                .ToArray();
            var processInstance = Activator.CreateInstance(objectType, parameters);
            if (processInstance != null)
                return processInstance;

            var processId = stepConfig != null ? stepConfig.ProcessId : string.Empty;
            throw new InvalidOperationException($"Failed to create process instance for step <{stepConfig?.Id}> with process ID <{processId}> and implementation <{objectType.FullName}>.");
        }

        /// <inheritdoc />
        public void Validate()
        {
            var (_, constructor, processParameterization) = PrepareProcessDescriptor();
            foreach (var parameterInfo in constructor.GetParameters())
            {
                ValidateParameter(parameterInfo, processParameterization);
            }
        }

        /// <summary>
        /// Resolves the process type, constructor, and merged parameterization for the
        /// currently configured step. Shared by <see cref="Build"/> and <see cref="Validate"/>
        /// so both paths surface the same errors at the same points; neither path has side
        /// effects here (no instance creation, no directory creation).
        /// </summary>
        private (Type ProcessType, ConstructorInfo Constructor, Parameterization Parameterization) PrepareProcessDescriptor()
        {
            ArgumentNullException.ThrowIfNull(stepConfig);
            ArgumentNullException.ThrowIfNull(processes);

            var processConfig = stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;

            if (processConfig == null)
                throw new InvalidOperationException($"No process config found for process ID <{stepConfig.ProcessId}>.");

            var objectType = GetProcessorType(processConfig.Implementation) ?? throw new InvalidOperationException($"Process <{processConfig.Implementation}> is unknown");

            var constructors = objectType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length != 1)
                throw new InvalidOperationException($"Process <{processConfig.Implementation}> has {constructors.Length} public constructors. A Process must have exactly one public constructor.");

            var constructor = constructors[0];
            var processBaseConfig = pipelineOptions.ProcessConfigs.GetValueOrDefault(processConfig.Implementation);
            var processParameterization = GetMergedParameterization(processBaseConfig, processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);
            return (objectType, constructor, processParameterization);
        }

        /// <summary>
        /// Mirror of <see cref="GenerateParameter"/> without the side-effectful materialization
        /// step (no <see cref="PipelineLogger"/> / <see cref="PipelineFileManager"/> allocation,
        /// no value conversion kept). Throws with the same message <see cref="GenerateParameter"/>
        /// would for an unsatisfiable non-nullable parameter.
        /// </summary>
        private static void ValidateParameter(ParameterInfo parameterInfo, Parameterization processConfig)
        {
            // Framework-provided dependencies are always satisfiable by Build (logger, file
            // manager, optional container runner). Skipping them here is how we avoid invoking
            // their constructors at startup.
            if (parameterInfo.ParameterType == typeof(ILogger) ||
                parameterInfo.ParameterType == typeof(IPipelineFileManager))
            {
                return;
            }

            if (!string.IsNullOrEmpty(parameterInfo.Name) &&
                processConfig.TryGetValue(parameterInfo.Name, out var rawValue) &&
                Parameterization.TryConvertObject(rawValue, parameterInfo.ParameterType, out _))
            {
                return;
            }

            if (IsParameterNullable(parameterInfo))
                return;

            throw new InvalidOperationException($"Process initialization: No suitable parameter found for parameter of type <{parameterInfo.ParameterType.Name}> and name <{parameterInfo.Name}>. Parameter is not nullable, cannot initialize process.");
        }

        private Type? GetProcessorType(string implementation)
        {
            if (implementation.StartsWith("Geopilot.Api.Pipeline.Process", StringComparison.Ordinal))
            {
                return Type.GetType(implementation);
            }
            else if (this.processorPluginAssemblies.Count > 0)
            {
                foreach (var assembly in this.processorPluginAssemblies)
                {
                    var type = assembly.GetType(implementation);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }

            logger.LogWarning($"For process implementation '{implementation}' no processor plugin configured. Cannot load process.");
            return null;
        }

        private object? GenerateParameter(ParameterInfo parameterInfo, Type processType, Parameterization processConfig, string pipelineDirectory, Guid jobId)
        {
            if (parameterInfo.ParameterType == typeof(ILogger))
            {
                return PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger(processType))
                    .PipelineId(pipelineId ?? string.Empty)
                    .StepId(stepConfig?.Id ?? string.Empty)
                    .JobId(jobId)
                    .Build();
            }
            else if (parameterInfo.ParameterType == typeof(IPipelineFileManager))
            {
                return new PipelineFileManager(pipelineDirectory, this.stepConfig?.Id ?? throw new InvalidOperationException("Step Id must be provided."));
            }
            else if (!string.IsNullOrEmpty(parameterInfo.Name) &&
                     processConfig.TryGetValue(parameterInfo.Name, out var rawValue) &&
                     Parameterization.TryConvertObject(rawValue, parameterInfo.ParameterType, out var convertedValue))
            {
                return convertedValue;
            }

            if (IsParameterNullable(parameterInfo))
                return null;
            else
                throw new InvalidOperationException($"Process initialization: No suitable parameter found for parameter of type <{parameterInfo.ParameterType.Name}> and name <{parameterInfo.Name}>. Parameter is not nullable, cannot initialize process.");
        }

        private static bool IsParameterNullable(ParameterInfo parameterInfo)
        {
            return new NullabilityInfoContext().Create(parameterInfo).WriteState is NullabilityState.Nullable;
        }

        private Parameterization GetMergedParameterization(Parameterization? processBaseConfig, Parameterization? processDefaultConfig, Parameterization? processDefaultConfigOverwrites)
        {
            var mergedParams = new Parameterization();

            // Start with processBaseConfig (lowest priority)
            if (processBaseConfig != null)
            {
                foreach (var config in processBaseConfig)
                {
                    mergedParams[config.Key] = config.Value;
                }
            }

            // Merge processDefaultConfig (medium priority)
            if (processDefaultConfig != null)
            {
                foreach (var config in processDefaultConfig)
                {
                    if (processBaseConfig != null && processBaseConfig.ContainsKey(config.Key))
                        throw new InvalidOperationException($"Conflict in process configuration: The key '{config.Key}' is defined in both process base configuration and process default configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten.");
                    else
                        mergedParams[config.Key] = config.Value;
                }
            }

            // Apply processDefaultConfigOverwrites (highest priority)
            if (processDefaultConfigOverwrites != null)
            {
                foreach (var overwrite in processDefaultConfigOverwrites)
                {
                    if (processBaseConfig != null && processBaseConfig.ContainsKey(overwrite.Key))
                        throw new InvalidOperationException($"Conflict in process configuration overwrite: The key '{overwrite.Key}' is defined in both process base configuration and process overwrite configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten.");
                    if (processDefaultConfig == null || !processDefaultConfig.ContainsKey(overwrite.Key))
                        throw new InvalidOperationException($"Conflict in process configuration overwrite: The key '{overwrite.Key}' is not defined in process default configuration, so it cannot be overwritten. Please ensure that only existing default configuration keys are overwritten.");
                    mergedParams[overwrite.Key] = overwrite.Value;
                }
            }

            return mergedParams;
        }
    }
}
