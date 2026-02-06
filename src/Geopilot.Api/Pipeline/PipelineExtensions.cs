using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
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

        if (pipelineProcessConfig.Processes == null || pipelineProcessConfig.Processes.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), "The Processes field is required."));
        else
            outputErrors = pipelineProcessConfig.Processes.Validate(outputErrors);

        if (pipelineProcessConfig.Pipelines == null || pipelineProcessConfig.Pipelines.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineProcessConfig), "The Pipelines field is required."));
        else
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

        outputErrors = processes.Aggregate(outputErrors, (errors, processConfig) => processConfig.Validate(errors));

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(this List<PipelineConfig> pipelines, List<ProcessConfig>? processes, PipelineValidationErrors? inputErrors = null)
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

        if (processConfig.Id == null || processConfig.Id.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), "The Id field is required."));

        if (processConfig.DataHandlingConfig == null)
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), "The DataHandlingConfig field is required."));

        if (processConfig.Implementation == null || processConfig.Implementation.Trim() == string.Empty)
        {
            outputErrors.Add(new PipelineValidationError(typeof(ProcessConfig), "The Implementation field is required."));
        }
        else
        {
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
        }

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(this PipelineConfig pipelineConfig, List<ProcessConfig>? processes, PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        if (pipelineConfig.Id == null || pipelineConfig.Id.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineConfig), "The Id field is required."));

        if (pipelineConfig.Parameters == null)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineConfig), "The Parameters field is required."));
        else
            outputErrors = pipelineConfig.Parameters.Validate(pipelineConfig.Id ?? "", outputErrors);

        if (pipelineConfig.Steps == null || pipelineConfig.Steps.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineConfig), "The Steps field is required."));
        else
            outputErrors = pipelineConfig.Steps.Validate(pipelineConfig, processes, outputErrors);

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this PipelineParametersConfig parameters,
        string pipelineId,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        if (parameters.UploadStep == null || parameters.UploadStep.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineParametersConfig), "The UploadStep field is required."));

        if (parameters.Mappings == null || parameters.Mappings.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(PipelineParametersConfig), "The Mappings field is required."));
        else
            outputErrors = parameters.Mappings.Validate(pipelineId, outputErrors);

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

        if (fileMapping.Attribute == null || fileMapping.Attribute.Trim() == string.Empty)
        {
            outputErrors.Add(new PipelineValidationError(typeof(FileMappingsConfig), "The Attribute field is required."));
        }

        if (fileMapping.FileExtension == null || fileMapping.FileExtension.Trim() == string.Empty)
        {
            outputErrors.Add(new PipelineValidationError(typeof(FileMappingsConfig), "The FileExtension field is required."));
        }
        else
        {
            var validFileExtension = new Regex(@"^[a-zA-Z]{2,}$");
            if (!validFileExtension.IsMatch(fileMapping.FileExtension))
            {
                outputErrors.Add(new PipelineValidationError(typeof(FileMappingsConfig), $"invalid file extension '{fileMapping.FileExtension}' in step '{pipelineId}'"));
            }
        }

        return outputErrors;
    }

    internal static PipelineValidationErrors Validate(
        this List<StepConfig> allPipelienSteps,
        PipelineConfig pipelineConfig,
        List<ProcessConfig>? processes,
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
        List<ProcessConfig>? processes,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        var processConfig = processes != null && stepConfig.ProcessId != null ? processes.GetProcessConfig(stepConfig.ProcessId) : null;

        if (stepConfig.Id == null || stepConfig.Id.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), "The Id field is required."));

        if (stepConfig.ProcessId == null || stepConfig.ProcessId.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), "The ProcessId field is required."));

        if (stepConfig.Input == null || stepConfig.Input.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), "The Input field is required."));

        if (stepConfig.Output == null || stepConfig.Output.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), "The Output field is required."));

        if (stepConfig.DisplayName == null || stepConfig.DisplayName.Count == 0)
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), "The DisplayName field is required."));

        if (processConfig == null)
        {
            outputErrors.Add(new PipelineValidationError(typeof(StepConfig), $"process reference for '{stepConfig.ProcessId}'"));
        }
        else
        {
            if (stepConfig.Input != null && stepConfig.Id != null)
            {
                var possibleStepConfigReferences = GatherPossibleStepConfigReferences(stepConfig.Id, pipelineConfig.Steps, pipelineConfig);
                outputErrors = stepConfig.Input.Validate(stepConfig.Id, possibleStepConfigReferences, processConfig, outputErrors);
            }

            if (stepConfig.Output != null)
            {
                outputErrors = stepConfig.Output.Validate(stepConfig.Id ?? "", processConfig, outputErrors);
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

        outputErrors = inputConfigs
            .Aggregate(outputErrors, (errors, inputConfig) => inputConfig.Validate(errors));

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

        if (processConfig.DataHandlingConfig != null && processConfig.DataHandlingConfig.InputMapping != null)
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
        this InputConfig inputConfig,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        if (inputConfig.From == null || inputConfig.From.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(InputConfig), "The From field is required."));

        if (inputConfig.Take == null || inputConfig.Take.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(InputConfig), "The Take field is required."));

        if (inputConfig.As == null || inputConfig.As.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(InputConfig), "The As field is required."));

        return outputErrors;
    }

    public static PipelineValidationErrors Validate(
        this List<OutputConfig> outputConfigs,
        string stepId,
        ProcessConfig processConfig,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        outputErrors = outputConfigs
            .Aggregate(outputErrors, (errors, outputConfig) => outputConfig.Validate(errors));

        if (processConfig.DataHandlingConfig != null && processConfig.DataHandlingConfig.OutputMapping != null)
        {
            outputConfigs.ForEach(outputConfig =>
            {
                var numberOfProcessOutput = processConfig.DataHandlingConfig.OutputMapping
                    .Where(o => o.Value == outputConfig.Take)
                    .Count();
                if (numberOfProcessOutput == 0)
                    outputErrors.Add(new PipelineValidationError(typeof(OutputConfig), $"illegal output take: '{outputConfig.Take}' in step '{stepId}'"));
            });
        }

        return outputErrors;
    }

    public static PipelineValidationErrors Validate(
        this OutputConfig outputConfig,
        PipelineValidationErrors? inputErrors = null)
    {
        var outputErrors = inputErrors != null ? new PipelineValidationErrors(inputErrors) : new PipelineValidationErrors();

        if (outputConfig.Take == null || outputConfig.Take.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(OutputConfig), "The Take field is required."));

        if (outputConfig.As == null || outputConfig.As.Trim() == string.Empty)
            outputErrors.Add(new PipelineValidationError(typeof(OutputConfig), "The As field is required."));

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
                Output = pipelineConfig.Parameters.Mappings != null ? pipelineConfig.Parameters.Mappings.Select(m => new OutputConfig() { As = m.Attribute, }).ToList() : new List<OutputConfig>(),
            };
            possibleStepConfigReferences.Add(uploadStepConfig);
        }

        possibleStepConfigReferences.AddRange(allPipelineSteps.TakeWhile(s => s.Id != currentStepId));

        return possibleStepConfigReferences;
    }
}
