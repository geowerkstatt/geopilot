using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
internal class PipelineFactory
{
    /// <summary>
    /// Creates a <see cref="PipelineFactory"/> instance from a YAML configuration.
    /// </summary>
    /// <param name="processDefinition">The YAML configuration string.</param>
    /// <returns>A <see cref="PipelineFactory"/> instance.</returns>
    public static PipelineFactory FromYaml(string processDefinition)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var pipelineProcessConfig = deserializer.Deserialize<PipelineProcessConfig>(processDefinition);
        return new PipelineFactory(pipelineProcessConfig);
    }

    /// <summary>
    /// Creates a <see cref="PipelineFactory"/> instance from a YAML file.
    /// </summary>
    /// <param name="path">The path to the YAML file.</param>
    /// <returns>A <see cref="PipelineFactory"/> instance.</returns>
    public static PipelineFactory FromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        return FromYaml(yaml);
    }

    private PipelineFactory(PipelineProcessConfig pipelineProcessConfig)
    {
        this.PipelineProcessConfig = pipelineProcessConfig;

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineFactory>();
    }

    private readonly ILogger<PipelineFactory> logger;

    public PipelineProcessConfig PipelineProcessConfig { get; }

    /// <summary>
    /// Creates a pipeline instance with the specified id.
    /// </summary>
    /// <param name="id">The id of the pipeline to be created. References to <see cref="PipelineConfig.Id"/>.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the pipeline cannot be created.</exception>
    internal Pipeline CreatePipeline(string id)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        if (pipelineConfig != null)
        {
            return new Pipeline(pipelineConfig.Id, pipelineConfig.DisplayName, CreateSteps(pipelineConfig), pipelineConfig.Parameters);
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    private List<PipelineStep> CreateSteps(PipelineConfig pipelineConfig)
    {
        return pipelineConfig.Steps
            .Select(CreateStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig)
    {
        return new PipelineStep(
            stepConfig.Id,
            stepConfig.DisplayName,
            stepConfig.Input ?? new List<InputConfig>(),
            stepConfig.Output ?? new List<OutputConfig>(),
            CreateProcess(stepConfig));
    }

    private IPipelineProcess CreateProcess(StepConfig stepConfig)
    {
        var processConfig = stepConfig.ProcessId != null ? PipelineProcessConfig.Processes.GetProcessConfig(stepConfig.ProcessId) : null;
        if (processConfig != null)
        {
            var objectType = Type.GetType(processConfig.Implementation);
            if (objectType != null)
            {
                var processInstance = Activator.CreateInstance(objectType) as IPipelineProcess;
                if (processInstance != null)
                {
                    processInstance.Name = processConfig.Id;
                    processInstance.DataHandlingConfig = processConfig.DataHandlingConfig;
                    processInstance.Config = GenerateProcessConfig(processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);
                    return processInstance;
                }
            }
        }

        throw new InvalidOperationException($"failed to create process instance for '{stepConfig.ProcessId}'");
    }

    private Dictionary<string, string> GenerateProcessConfig(Dictionary<string, string>? processDefaultConfig, Dictionary<string, string>? processDefaultConfigOverwrites)
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
