using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Options;
using System.Reflection;
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
                processorPluginAssemblies.Clear();
                foreach (var processorPluginLoadContext in processorPluginLoadContexts)
                {
                    processorPluginLoadContext.Unload();
                }
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
    /// <param name="logger">Logger instance for logging factory operations. Cannot be null.</param>
    public PipelineProcessFactory(IOptions<PipelineOptions> pipelinePluginOptions, ILogger<PipelineProcessFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(pipelinePluginOptions);

        this.logger = logger;
        this.pipelineOptions = pipelinePluginOptions.Value;
        var processorPlugins = pipelineOptions.Plugins;

        if (processorPlugins != null)
        {
            foreach (var assemblyPath in processorPlugins)
            {
                var assemblyFullPath = Path.IsPathRooted(assemblyPath) ? assemblyPath : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, assemblyPath));

                var assemblyContext = new AssemblyLoadContext(assemblyFullPath);
                var plugin = assemblyContext.LoadFromAssemblyPath(assemblyFullPath);
                processorPluginAssemblies.Add(plugin);
                processorPluginLoadContexts.Add(assemblyContext);
            }
        }
        else
        {
            this.processorPluginAssemblies = new HashSet<Assembly>();
        }
    }

    /// <inheritdoc />
    public object CreateProcess(StepConfig stepConfig, List<ProcessConfig> processes)
    {
        ArgumentNullException.ThrowIfNull(stepConfig);

        var processConfig = stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;

        string processImplementation = "unknown";
        if (processConfig != null)
        {
            processImplementation = processConfig.Implementation;
            var objectType = GetProccessorType(processImplementation);
            if (objectType != null)
            {
                var processInstance = Activator.CreateInstance(objectType);
                if (processInstance != null)
                {
                    var processBaseConfig = pipelineOptions.ProcessConfigs.GetValueOrDefault(processConfig.Implementation);
                    var processParameterization = GetMergedParameterization(processBaseConfig, processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);
                    InitializeProcess(objectType, processInstance, processParameterization);
                    return processInstance;
                }
            }
        }

        var processId = stepConfig != null ? stepConfig.ProcessId : string.Empty;
        throw new InvalidOperationException($"Failed to create process instance for step <{stepConfig?.Id}> with process ID <{processId}> and implementation <{processImplementation}>.");
    }

    private Type? GetProccessorType(string implementation)
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

    private void InitializeProcess(Type processType, object process, Parameterization processParams)
    {
        var initMethods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessInitializeAttribute)))
            .ToList();
        initMethods
            .ForEach(m =>
            {
                var parameters = m.GetParameters()
                    .Select(p => GenerateParameter(p, processType, processParams))
                    .ToArray();
                m.Invoke(process, parameters);
            });
    }

    private object? GenerateParameter(ParameterInfo parameterInfo, Type processType, Parameterization processConfig)
    {
        if (parameterInfo.ParameterType.IsAssignableFrom(processConfig.GetType()))
        {
            object param = processConfig; // Only required because of a compiler warning. Won't be necessary as soon as there is another mapped parameter type.
            return param;
        }
        else if (parameterInfo.ParameterType == typeof(ILogger))
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            return factory.CreateLogger(processType);
        }
        else if (!string.IsNullOrEmpty(parameterInfo.Name) && processConfig.TryGetValue(parameterInfo.Name, out var parameterStringValue))
        {
            if (parameterInfo.ParameterType == typeof(string))
            {
                return parameterStringValue;
            }
            else if (int.TryParse(parameterStringValue, out var parameterIntValue) && parameterInfo.ParameterType.IsAssignableFrom(parameterIntValue.GetType()))
            {
                return parameterIntValue;
            }
            else if (double.TryParse(parameterStringValue, out var parameterDoubleValue) && parameterInfo.ParameterType.IsAssignableFrom(parameterDoubleValue.GetType()))
            {
                return parameterDoubleValue;
            }
            else if (bool.TryParse(parameterStringValue, out var parameterBoolValue) && parameterInfo.ParameterType.IsAssignableFrom(parameterBoolValue.GetType()))
            {
                return parameterBoolValue;
            }
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
                mergedParams[config.Key] = config.Value;
            }
        }

        // Apply processDefaultConfigOverwrites (highest priority)
        if (processDefaultConfigOverwrites != null)
        {
            foreach (var overwrite in processDefaultConfigOverwrites)
            {
                if (processDefaultConfig != null && processDefaultConfig.ContainsKey(overwrite.Key))
                {
                    mergedParams[overwrite.Key] = overwrite.Value;
                }
                else
                {
                    this.logger.LogWarning("Attempted to overwrite a process configuration that is not defined in default configuration of process: '{Key}' ==> '{Value}'", overwrite.Key, overwrite.Value);
                }
            }
        }

        return mergedParams;
    }
}
