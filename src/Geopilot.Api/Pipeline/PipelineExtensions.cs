using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Name == processName);
    }

    internal static PipelineValidationErrors Validate(this PipelineProcessConfig pipelineProcessConfig, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var duplicatePipelineNames = string.Join(", ", pipelineProcessConfig.Pipelines
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicatePipelineNames))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate pipeline names found: {duplicatePipelineNames}"));

        var duplicateProcessNames = string.Join(", ", pipelineProcessConfig.Processes
            .GroupBy(p => p.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateProcessNames))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate process names found: {duplicateProcessNames}"));

        outputErrors = pipelineProcessConfig.Processes
            .Aggregate(outputErrors, (errors, process) => process.Validate(errors));

        outputErrors = pipelineProcessConfig.Pipelines
            .Aggregate(outputErrors, (errors, pipeline) => pipeline.Validate(pipelineProcessConfig.Processes, errors));

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(this ProcessConfig processConfig, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();
        var objectType = Type.GetType(processConfig.Implementation);
        if (objectType != null)
        {
            if (objectType.GetConstructor(Type.EmptyTypes) == null)
                outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), $"no parameterless constructor found for process implementation '{processConfig.Implementation}'"));

            var processInstance = Activator.CreateInstance(objectType) as IPipelineProcess;

            if (processInstance == null)
                outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), $"failed to create process instance for '{processConfig.Implementation}'"));
        }
        else
        {
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), $"unknown implementation '{processConfig.Implementation}' for process '{processConfig.Name}'"));
        }

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this PipelineConfig pipelineConfig, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = pipelineConfig.Steps
            .Aggregate(inputErrors, (errors, pipeline) => pipeline.Validate(processes, errors));

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this StepConfig stepConfig, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var processConfig = processes.GetProcessConfig(stepConfig.Process);
        if (processConfig == null)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), $"process reference for '{stepConfig.Process}'"));

        return outputErrors ?? new PipelineValidationErrors();
    }
}
