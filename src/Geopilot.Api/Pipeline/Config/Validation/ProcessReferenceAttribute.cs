using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ProcessReferenceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not PipelineProcessConfig pipelineProcessConfig)
        {
            return false;
        }

        if (pipelineProcessConfig.Pipelines.Count == 0)
        {
            return true;
        }

        var steps = pipelineProcessConfig
            .Pipelines
            .Where(p => p.Steps != null)
            .SelectMany(p => p.Steps)
            .ToList();

        if (steps == null || steps.Count == 0)
        {
            return true;
        }

        var missingProcessReferences = steps
            .Select(s => s.ProcessId)
            .Where(processId => !string.IsNullOrEmpty(processId))
            .Where(processId => !pipelineProcessConfig.Processes.Any(p => p.Id == processId));
        if (missingProcessReferences != null && missingProcessReferences.Any())
        {
            ErrorMessage = $"One or more steps reference a process that is not defined in the processes collection: {string.Join(", ", missingProcessReferences)}.";
            return false;
        }
        else
        {
            return true;
        }
    }
}
