using MathNet.Numerics.Distributions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class StepInputReferenceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not PipelineConfig pipeline)
        {
            return false;
        }

        var allSteps = pipeline.Steps ?? new List<StepConfig>();
        if (allSteps == null || allSteps.Count == 0)
        {
            return true;
        }

        return allSteps.All(s => Validate(s, pipeline.Parameters, allSteps));
    }

    private bool Validate(StepConfig stepToValidate, PipelineParametersConfig pipelineParameters, List<StepConfig> allSteps)
    {
        if (stepToValidate.Input == null)
        {
            return true;
        }

        var errorMessages = new List<string>();
        foreach (var input in stepToValidate.Input)
        {
            var hasReferenceFromAStep = HasStep(input.From, input.Take, stepToValidate.Id, allSteps);
            var hasReferenceFromPipelineParameters = HasStep(input.From, input.Take, pipelineParameters);
            if (!hasReferenceFromAStep && !hasReferenceFromPipelineParameters)
            {
                errorMessages.Add($"Step '{stepToValidate.Id}' has an input reference to '{input.From}' with attribute '{input.Take}' that cannot be found in previous steps or pipeline parameters.");
            }
        }

        if (errorMessages.Count == 0)
        {
            return true;
        }
        else
        {
            this.ErrorMessage = string.Join(Environment.NewLine, errorMessages);
            return false;
        }
    }

    private bool HasStep(string stepId, string take, string currentStepId, List<StepConfig> allSteps)
    {
        return allSteps.TakeWhile(s => s.Id != currentStepId).Any(s => s.Id == stepId && HasOutput(s.Output, take));
    }

    private bool HasOutput(List<OutputConfig>? outputConfig, string take)
    {
        if (outputConfig != null)
        {
            return outputConfig.Any(o => o.Take == take);
        }
        else
        {
            // we return true as the step has a required annotation on the output
            // and we don't want to fail twice on the same issue (missing output reference)
            return true;
        }
    }

    private bool HasStep(string stepId, string attribute, PipelineParametersConfig pipelineParameters)
    {
        if (pipelineParameters != null &&
            pipelineParameters.Mappings != null &&
            pipelineParameters.UploadStep == stepId &&
            pipelineParameters.Mappings.Any(m => m.Attribute == attribute))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
