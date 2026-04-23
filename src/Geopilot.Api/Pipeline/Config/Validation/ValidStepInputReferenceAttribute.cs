using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Pipeline.Config.Validation;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class ValidStepInputReferenceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not PipelineConfig pipeline)
        {
            return new ValidationResult("validation object is not of type PipelineConfig");
        }

        var allSteps = pipeline.Steps ?? new List<StepConfig>();
        if (allSteps == null || allSteps.Count == 0)
        {
            return ValidationResult.Success;
        }

        var errorMessages = allSteps.SelectMany(s => GetErrorMessages(s, allSteps)).ToList();

        if (errorMessages.Count > 0)
        {
            return new ValidationResult(string.Join(Environment.NewLine, errorMessages));
        }
        else
        {
            return ValidationResult.Success;
        }
    }

    private List<string> GetErrorMessages(StepConfig stepToValidate, List<StepConfig> allSteps)
    {
        if (stepToValidate.Input == null)
        {
            return new List<string>();
        }

        var errorMessages = new List<string>();
        foreach (var input in stepToValidate.Input)
        {
            var hasReferenceFromAStep = HasStep(input.From, input.Take, stepToValidate.Id, allSteps);
            if (!hasReferenceFromAStep)
            {
                errorMessages.Add($"Step '{stepToValidate.Id}' has an input reference to '{input.From}' with attribute '{input.Take}' that cannot be found in previous steps.");
            }
        }

        return errorMessages;
    }

    private bool HasStep(string stepId, string take, string currentStepId, List<StepConfig> allSteps)
    {
        return allSteps.TakeWhile(s => s.Id != currentStepId).Any(s => s.Id == stepId && HasOutput(s.Output, take));
    }

    private bool HasOutput(List<OutputConfig>? outputConfig, string take)
    {
        if (outputConfig != null)
        {
            return outputConfig.Any(o => o.As == take);
        }
        else
        {
            // we return true as the step has a required annotation on the output
            // and we don't want to fail twice on the same issue (missing output reference)
            return true;
        }
    }
}
