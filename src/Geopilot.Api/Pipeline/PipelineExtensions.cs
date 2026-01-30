using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Pipeline;

internal static class PipelineExtensions
{
    internal static ProcessConfig? GetProcessConfig(this List<ProcessConfig> processes, string processName)
    {
        return processes.FirstOrDefault(p => p.Id == processName);
    }

    internal static PipelineValidationErrors Validate(this PipelineProcessConfig pipelineProcessConfig, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = pipelineProcessConfig.Processes.Validate(outputErrors);

        outputErrors = pipelineProcessConfig.Pipelines.Validate(pipelineProcessConfig.Processes, outputErrors);

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(this List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var duplicateProcessIds = string.Join(", ", processes
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateProcessIds))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate process ids found: {duplicateProcessIds}"));

        outputErrors = processes.Aggregate(outputErrors, (errors, process) => process.Validate(errors));

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this List<PipelineConfig> pipelines, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var duplicatePipelineIds = string.Join(", ", pipelines
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicatePipelineIds))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate pipeline ids found: {duplicatePipelineIds}"));

        outputErrors = pipelines.Aggregate(outputErrors, (errors, pipeline) => pipeline.Validate(processes, errors));

        return outputErrors ?? new PipelineValidationErrors();
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
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), $"unknown implementation '{processConfig.Implementation}' for process '{processConfig.Id}'"));
        }

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this PipelineConfig pipelineConfig, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = pipelineConfig.Steps.Validate(processes, outputErrors);

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this List<StepConfig> steps, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var duplicateStepIds = string.Join(", ", steps
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateStepIds))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate step ids found: {duplicateStepIds}"));

        outputErrors = steps
            .Aggregate(outputErrors, (errors, pipeline) => pipeline.Validate(processes, errors));

        return outputErrors ?? new PipelineValidationErrors();
    }

    internal static PipelineValidationErrors Validate(this StepConfig stepConfig, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var processConfig = processes.GetProcessConfig(stepConfig.ProcessId);
        if (processConfig == null)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), $"process reference for '{stepConfig.ProcessId}'"));

        return outputErrors ?? new PipelineValidationErrors();
    }
}
