using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;
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
public class PipelineProcessFactory : AssemblyLoadContext, IPipelineProcessFactory
{
    private readonly ILogger<PipelineProcessFactory> logger;

    private IConfiguration configuration;
    private HashSet<Assembly> processorPluginAssemblies;

    /// <summary>
    /// Initializes a new instance of the PipelineProcessFactory class using the specified configuration settings.
    /// </summary>
    /// <remarks>The constructor loads plugin assemblies specified in the "Pipeline:Plugins" section of the
    /// configuration. Assemblies are loaded into a dedicated context, allowing for isolation and dynamic plugin
    /// management. If no plugins are configured, the factory will operate without any loaded assemblies.</remarks>
    /// <param name="configuration">The configuration source containing pipeline and plugin settings. Cannot be null.</param>
    public PipelineProcessFactory(IConfiguration configuration)
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineProcessFactory>();
        this.configuration = configuration;
        var processorPlugins = this.configuration.GetSection("Pipeline:Plugins").Get<List<string>>();

        if (processorPlugins != null)
        {
            this.processorPluginAssemblies = processorPlugins
                .Select(p =>
                {
                    if (Path.IsPathRooted(p))
                        return p;
                    else
                        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, p));
                })
                .Select(LoadFromAssemblyPath)
                .ToHashSet();
        }
        else
        {
            this.processorPluginAssemblies = new HashSet<Assembly>();
        }
    }

    /// <inheritdoc />
    public object CreateProcess(StepConfig stepConfig, List<ProcessConfig> processes)
    {
        var processConfig = stepConfig != null && stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;
        if (processConfig != null)
        {
            var objectType = GetProccessorType(processConfig.Implementation);
            if (objectType != null)
            {
                var processInstance = Activator.CreateInstance(objectType);
                if (processInstance != null)
                {
                    InitializeProcess(objectType, processInstance, GenerateProcessConfig(processConfig.DefaultConfig, stepConfig != null ? stepConfig.ProcessConfigOverwrites : new Parameterization()));

                    return processInstance;
                }
            }
        }

        var processId = stepConfig != null ? stepConfig.ProcessId : string.Empty;
        throw new InvalidOperationException($"failed to create process instance for '{processId}'");
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

    private void InitializeProcess(Type processType, object process, Dictionary<string, string> processConfig)
    {
        var initMethods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessInitializeAttribute)))
            .ToList();
        initMethods
            .ForEach(m =>
            {
                var parameters = m.GetParameters()
                    .Select(p => p.ParameterType)
                    .Select(t => GenerateParameter(t, processConfig))
                    .ToArray();
                m.Invoke(process, parameters);
            });
    }

    private object? GenerateParameter(Type parameterType, Dictionary<string, string> processConfig)
    {
        if (parameterType == typeof(IConfiguration))
        {
            return configuration;
        }
        else if (parameterType == typeof(Dictionary<string, string>))
        {
            return processConfig;
        }
        else
        {
            logger.LogWarning($"Process initialization: No suitable parameter found for type '{parameterType}' with name '{parameterType.Name}' Initializing with null.");
            return null;
        }
    }

    private Dictionary<string, string> GenerateProcessConfig(Parameterization? processDefaultConfig, Parameterization? processDefaultConfigOverwrites)
    {
        var mergedConfig = processDefaultConfig != null ? new Dictionary<string, string>(processDefaultConfig) : new Dictionary<string, string>();
        if (processDefaultConfigOverwrites != null)
        {
            foreach (var overwrite in processDefaultConfigOverwrites)
            {
                if (mergedConfig.ContainsKey(overwrite.Key))
                {
                    mergedConfig[overwrite.Key] = overwrite.Value;
                }
                else
                {
                    this.logger.LogWarning("Attempted to overwrite non-existing process configuration '{Key}' ==> '{Value}'", overwrite.Key, overwrite.Value);
                }
            }
        }

        return mergedConfig;
    }
}
