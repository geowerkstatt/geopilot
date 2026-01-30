using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Globalization;
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
            .WithNodeDeserializer(i => new DeserializerValidation(i), s => s.InsteadOf<ObjectNodeDeserializer>())
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
        this.pipelineProcessConfig = pipelineProcessConfig;

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineFactory>();
    }

    private readonly ILogger<PipelineFactory> logger;
    private PipelineProcessConfig pipelineProcessConfig;

    /// <summary>
    /// Creates a pipeline instance with the specified name.
    /// </summary>
    /// <param name="name">The name of the pipeline. References to <see cref="PipelineConfig.Name"/>.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the pipeline cannot be created.</exception>
    internal Pipeline CreatePipeline(string name)
    {
        var duplicatePipelineNames = string.Join(", ", this.pipelineProcessConfig.Pipelines
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicatePipelineNames))
            throw new InvalidOperationException($"duplicate pipeline names found: {duplicatePipelineNames}");

        var duplicateProcessNames = string.Join(", ", this.pipelineProcessConfig.Processes
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateProcessNames))
            throw new InvalidOperationException($"duplicate process names found: {duplicateProcessNames}");

        var pipelineConfig = this.pipelineProcessConfig.Pipelines.Find(p => p.Name == name);
        if (pipelineConfig == null)
            throw new InvalidOperationException($"pipeline for '{name}' not found");

        return new Pipeline(pipelineConfig.Name, CreateSteps(pipelineConfig), pipelineConfig.Parameters);
    }

    private List<PipelineStep> CreateSteps(PipelineConfig pipelineConfig)
    {
        return pipelineConfig.Steps
            .Select(CreateStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig)
    {
        return new PipelineStep(stepConfig.Name, stepConfig.Input, stepConfig.Output, CreateProcess(stepConfig));
    }

    private ProcessConfig? GetProcessConfig(string processName)
    {
        return this.pipelineProcessConfig.Processes
            .FirstOrDefault(p => p.Name == processName);
    }

    private IPipelineProcess CreateProcess(StepConfig stepConfig)
    {
        var processConfig = GetProcessConfig(stepConfig.Process);
        if (processConfig == null)
            throw new InvalidOperationException($"process reference for '{stepConfig.Process}'");
        var objectType = Type.GetType(processConfig.Implementation);
        if (objectType == null)
            throw new InvalidOperationException($"unknown implementation '{processConfig.Implementation}' for process '{stepConfig.Process}'");
        if (objectType.GetConstructor(Type.EmptyTypes) == null)
            throw new InvalidOperationException($"no parameterless constructor found for process implementation '{processConfig.Implementation}'");
        var processInstance = Activator.CreateInstance(objectType) as IPipelineProcess;
        if (processInstance == null)
            throw new InvalidOperationException("failed to create process instance for '{stepConfig.Process}'");

        processInstance.Name = processConfig.Name;
        processInstance.DataHandlingConfig = processConfig.DataHandlingConfig;
        processInstance.Config = GenerateProcessConfig(processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);

        return processInstance;
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
