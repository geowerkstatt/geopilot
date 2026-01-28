using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Stac;
using System.Diagnostics;
using System.Text.RegularExpressions;

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

        return outputErrors;
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
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), $"unknown implementation '{processConfig.Implementation}' for process '{processConfig.Id}'"));
        }

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(this PipelineConfig pipelineConfig, List<ProcessConfig> processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = pipelineConfig.Steps.Validate(pipelineConfig, processes, outputErrors);
        outputErrors = pipelineConfig.Parameters.Validate(pipelineConfig.Id, outputErrors);

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this PipelineParametersConfig parameters,
        string stepId,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = parameters.Mappings.Validate(stepId, outputErrors);

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this List<FileMappingsConfig> fileMappings,
        string pipelineId,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = fileMappings.Aggregate(outputErrors, (errors, fileMapping) => fileMapping.Validate(pipelineId, errors));

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this FileMappingsConfig fileMapping,
        string pipelineId,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var validFileExtension = new Regex(@"^[a-zA-Z]{2,}$");
        if (!validFileExtension.IsMatch(fileMapping.FileExtension))
        {
            outputErrors.Add(new PipelineValidationError(typeof(FileMappingsConfig), $"invalid file extension '{fileMapping.FileExtension}' in step '{pipelineId}'"));
        }

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this List<StepConfig> allPipelienSteps,
        PipelineConfig pipelineConfig,
        List<ProcessConfig> processes,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var duplicateStepIds = string.Join(", ", allPipelienSteps
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key));
        if (!string.IsNullOrEmpty(duplicateStepIds))
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), $"duplicate step ids found: {duplicateStepIds}"));

        outputErrors = allPipelienSteps
            .Aggregate(outputErrors, (errors, step) => step.Validate(pipelineConfig, processes, errors));

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this StepConfig stepConfig,
        PipelineConfig pipelineConfig,
        List<ProcessConfig> processes,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var processConfig = stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;

        if (processConfig == null)
        {
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), $"process reference for '{stepConfig.ProcessId}'"));
        }
        else
        {
            outputErrors = processConfig.Validate(outputErrors);
            if (stepConfig.Input != null)
            {
                var possibleStepConfigReferences = GatherPossibleStepConfigReferences(stepConfig.Id, pipelineConfig.Steps, pipelineConfig);
                outputErrors = stepConfig.Input.Validate(stepConfig.Id, possibleStepConfigReferences, processConfig, outputErrors);
            }

            if (stepConfig.Output != null)
            {
                outputErrors = stepConfig.Output.Validate(stepConfig.Id, processConfig, outputErrors);
            }
        }

        if (stepConfig.Output != null)
        {
            var duplicateOutputAs = string.Join(", ", stepConfig.Output
                .GroupBy(p => p.As)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key));
            if (!string.IsNullOrEmpty(duplicateOutputAs))
                outputErrors.Add(new PipelineValidationError(typeof(OutputConfig), $"not unique output as: '{duplicateOutputAs}' in step 'validation'"));
        }

        return outputErrors;
    }

    public static PipelineValidationErrors Validate(
        this List<InputConfig> inputConfigs,
        string stepId,
        List<StepConfig> possibleStepRefenreces,
        ProcessConfig processConfig,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        inputConfigs.ForEach(inputConfig =>
        {
            var matchingReferences = possibleStepRefenreces
                .Where(s => inputConfig.From == s.Id && s.Output != null && s.Output.Where(o => o.As == inputConfig.Take).Any());
            if (!matchingReferences.Any())
            {
                outputErrors.Add(new PipelineValidationError(typeof(InputConfig), $"illegal input from reference from: '{inputConfig.From}', take: '{inputConfig.Take}' in step '{stepId}'"));
            }
            else if (matchingReferences.Count() > 1)
            {
                outputErrors.Add(new PipelineValidationError(typeof(InputConfig), $"ambiguous input from reference from: '{inputConfig.From}', take: '{inputConfig.Take}' in step '{stepId}'"));
            }
        });

        if (processConfig.DataHandlingConfig.InputMapping != null)
        {
            inputConfigs.ForEach(inputConfig =>
            {
                var numberOfProcessInput = processConfig.DataHandlingConfig.InputMapping
                    .Where(i => i.Value == inputConfig.As)
                    .Count();
                if (numberOfProcessInput == 0)
                    outputErrors.Add(new PipelineValidationError(typeof(InputConfig), $"illegal input as: '{inputConfig.As}' in step '{stepId}'"));
            });
        }

        return outputErrors;
    }

    public static PipelineValidationErrors Validate(
        this List<OutputConfig> outputConfigs,
        string stepId,
        ProcessConfig processConfig,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        if (processConfig.DataHandlingConfig.OutputMapping != null)
        {
            outputConfigs.ForEach(outputConfig =>
            {
                var numberOfProcessOutput = processConfig.DataHandlingConfig.OutputMapping
                    .Where(o => o.Value == outputConfig.Take)
                    .Count();
                if (numberOfProcessOutput == 0)
                    outputErrors.Add(new PipelineValidationError(typeof(OutputConfig), $"illegal output take: '{outputConfig.As}' in step '{stepId}'"));
            });
        }

        return outputErrors;
    }

    private static List<StepConfig> GatherPossibleStepConfigReferences(
        string currentStepId,
        List<StepConfig> allPipelineSteps,
        PipelineConfig pipelineConfig)
    {
        var possibleStepConfigReferences = new List<StepConfig>();

        if (pipelineConfig != null && pipelineConfig.Parameters != null && !string.IsNullOrEmpty(pipelineConfig.Parameters.UploadStep))
        {
            var uploadStepConfig = new StepConfig()
            {
                Id = pipelineConfig.Parameters.UploadStep,
                DisplayName = pipelineConfig.DisplayName,
                Output = pipelineConfig.Parameters.Mappings
                .Select(m => new OutputConfig() { As = m.Attribute, }).ToList(),
            };
            possibleStepConfigReferences.Add(uploadStepConfig);
        }

        possibleStepConfigReferences.AddRange(allPipelineSteps.TakeWhile(s => s.Id != currentStepId));

        return possibleStepConfigReferences;
    }
}
