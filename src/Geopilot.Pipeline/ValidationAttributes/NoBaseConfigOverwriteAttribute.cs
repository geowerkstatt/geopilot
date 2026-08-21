using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.ValidationAttributes;

/// <summary>
/// Rejects a pipeline definition that sets a process configuration key which the hosting base
/// configuration already pins, on either a process <c>default_config</c> or a step's
/// <c>process_config_overwrites</c>. The base configuration is keyed by process implementation, so one
/// base entry covers every process that names it. The check is skipped when no base configuration
/// reaches the validation through <see cref="ValidationContext.Items"/>: without it no collision can
/// exist, and a plugin building a pipeline from its own definition has no hosting layer.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class NoBaseConfigOverwriteAttribute : ValidationAttribute
{
    /// <summary>
    /// Key under which the caller of the validation places the base configuration of every process
    /// implementation, taken from <c>Pipeline:ProcessConfigs</c>.
    /// </summary>
    internal const string BaseConfigsKey = "ProcessBaseConfigs";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return new ValidationResult("validation object is not of type PipelineProcessConfig");
        }

        if (!validationContext.Items.TryGetValue(BaseConfigsKey, out var baseConfigsItem) ||
            baseConfigsItem is not IReadOnlyDictionary<string, Parameterization> baseConfigs ||
            baseConfigs.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errorMessages = CollectDefaultConfigCollisions(pipelineProcessConfig, baseConfigs)
            .Concat(CollectStepOverwriteCollisions(pipelineProcessConfig, baseConfigs))
            .ToList();

        if (errorMessages.Count > 0)
        {
            return new ValidationResult(string.Join(Environment.NewLine, errorMessages));
        }
        else
        {
            return ValidationResult.Success;
        }
    }

    private static IEnumerable<string> CollectDefaultConfigCollisions(
        PipelineProcessConfig pipelineProcessConfig,
        IReadOnlyDictionary<string, Parameterization> baseConfigs)
    {
        foreach (var process in pipelineProcessConfig.Processes)
        {
            if (process.DefaultConfig == null || !baseConfigs.TryGetValue(process.Implementation, out var baseConfig))
                continue;

            foreach (var key in process.DefaultConfig.Keys.Where(baseConfig.ContainsKey))
            {
                yield return ProcessConfigCollision.InDefaultConfig(process.Implementation, process.Id, key);
            }
        }
    }

    private static IEnumerable<string> CollectStepOverwriteCollisions(
        PipelineProcessConfig pipelineProcessConfig,
        IReadOnlyDictionary<string, Parameterization> baseConfigs)
    {
        foreach (var pipeline in pipelineProcessConfig.Pipelines)
        {
            if (pipeline.Steps == null)
                continue;

            foreach (var step in pipeline.Steps)
            {
                if (step.ProcessConfigOverwrites == null || string.IsNullOrEmpty(step.ProcessId))
                    continue;

                var process = pipelineProcessConfig.Processes.GetProcessConfig(step.ProcessId);
                if (process == null || !baseConfigs.TryGetValue(process.Implementation, out var baseConfig))
                    continue;

                foreach (var key in step.ProcessConfigOverwrites.Keys.Where(baseConfig.ContainsKey))
                {
                    yield return ProcessConfigCollision.InStepOverwrite(process.Implementation, pipeline.Id, step.Id, key);
                }
            }
        }
    }
}
