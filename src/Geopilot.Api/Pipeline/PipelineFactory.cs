using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Stac;
using System;
using System.Diagnostics;
using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        this.pipelineProcessConfig = pipelineProcessConfig;
    }

    private PipelineProcessConfig pipelineProcessConfig;

    /// <summary>
    /// Creates a pipeline instance with the specified name.
    /// </summary>
    /// <param name="name">The name of the pipeline. References to <see cref="PipelineConfig.Name"/>.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline cannot be created.</exception>
    internal Pipeline CreatePipeline(string name)
    {
        if (this.pipelineProcessConfig.Pipelines == null)
            throw new InvalidOperationException("no pipelines defined");
        if (this.pipelineProcessConfig.Processes == null)
            throw new InvalidOperationException("no processes defined");

        var duplicatePipelineNames = string.Join(", ", this.pipelineProcessConfig.Pipelines
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicatePipelineNames))
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "duplicate pipeline names found: {0}", duplicatePipelineNames));

        var duplicateProcessNames = string.Join(", ", this.pipelineProcessConfig.Processes
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateProcessNames))
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "duplicate process names found: {0}", duplicateProcessNames));

        var pipelineConfig = this.pipelineProcessConfig.Pipelines.Find(p => p.Name == name);
        if (pipelineConfig == null)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "pipeline for '{0}' not found", name));

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
        var process = stepConfig.Process;
        if (string.IsNullOrEmpty(process))
            throw new InvalidOperationException("no process defined in step");
        var processConfig = GetProcessConfig(process);
        if (processConfig == null)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "process type for '{0}' not found", process));
        var objectType = Type.GetType(processConfig.Implementation);
        if (objectType == null)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "unknown implementation '{0}' for process '{1}'", processConfig.Implementation, process));
        var processInstance = Activator.CreateInstance(objectType) as IPipelineProcess;
        if (processInstance == null)
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "failed to create process instance for '{0}'", process));

        processInstance.Name = processConfig.Name;
        processInstance.DataHandlingConfig = processConfig.DataHandlingConfig;
        processInstance.Config = GenerateProcessConfig(processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites);

        return processInstance;
    }

    private Dictionary<string, string> GenerateProcessConfig(Dictionary<string, string> processDefaultConfig, Dictionary<string, string>? processDefaultConfigOverwrites)
    {
        var mergedConfig = new Dictionary<string, string>(processDefaultConfig);
        if (processDefaultConfigOverwrites != null)
        {
            foreach (var overwrite in processDefaultConfigOverwrites)
            {
                if (mergedConfig.ContainsKey(overwrite.Key))
                {
                    mergedConfig[overwrite.Key] = overwrite.Value;
                }
            }
        }

        return mergedConfig;
    }
}
