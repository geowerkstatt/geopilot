using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class NoUndefinedBaseConfigOverwriteAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return new ValidationResult("validation object is not of type PipelineProcessConfig");
        }

        if (pipelineProcessConfig.Pipelines.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errorMessages = new List<string>();
        foreach (var pipeline in pipelineProcessConfig.Pipelines)
        {
            if (pipeline.Steps == null)
                continue;
            foreach (var step in pipeline.Steps)
            {
                if (step.ProcessConfigOverwrites == null)
                    continue;
                if (string.IsNullOrEmpty(step.ProcessId))
                    continue;
                var defaultConfig = GetDefaultConfig(step.ProcessId, pipelineProcessConfig.Processes);
                foreach (var stepOverwrites in step.ProcessConfigOverwrites)
                {
                    if (!defaultConfig.ContainsKey(stepOverwrites.Key))
                    {
                        errorMessages.Add($"'Step '{step.Id}' in pipeline '{pipeline.Id}' is trying to overwrite process config parameter '{stepOverwrites.Key}' which is not defined in the default config.");
                    }
                }
            }
        }

        if (errorMessages.Count > 0)
        {
            return new ValidationResult(string.Join(Environment.NewLine, errorMessages));
        }
        else
        {
            return ValidationResult.Success;
        }
    }

    private Parameterization GetDefaultConfig(string stepId, List<ProcessConfig> processes)
    {
        var process = processes.FirstOrDefault(p => processes.Any(s => s.Id == stepId));
        if (process != null && process?.DefaultConfig != null)
            return process.DefaultConfig;
        else
            return new Parameterization();
    }
}
