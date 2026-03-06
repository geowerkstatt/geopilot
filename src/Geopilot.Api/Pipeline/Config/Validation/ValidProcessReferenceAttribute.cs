using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ValidProcessReferenceAttribute : ValidationAttribute
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

        var steps = pipelineProcessConfig
            .Pipelines
            .Where(p => p.Steps != null)
            .SelectMany(p => p.Steps)
            .ToList();

        if (steps == null || steps.Count == 0)
        {
            return ValidationResult.Success;
        }

        var missingProcessReferences = steps
            .Select(s => s.ProcessId)
            .Where(processId => !string.IsNullOrEmpty(processId))
            .Where(processId => !pipelineProcessConfig.Processes.Any(p => p.Id == processId));

        if (missingProcessReferences != null && missingProcessReferences.Any())
        {
            return new ValidationResult($"One or more steps reference a process that is not defined in the processes collection: {string.Join(", ", missingProcessReferences)}.");
        }
        else
        {
            return ValidationResult.Success;
        }
    }
}
