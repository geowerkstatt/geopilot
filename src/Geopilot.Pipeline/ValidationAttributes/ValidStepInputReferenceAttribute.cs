using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.ValidationAttributes;

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

    private static List<string> GetErrorMessages(StepConfig stepToValidate, List<StepConfig> allSteps)
    {
        if (stepToValidate.Input == null)
        {
            return new List<string>();
        }

        IReadOnlyDictionary<string, InputValue> compiledInput;
        try
        {
            compiledInput = InputCompiler.Compile(stepToValidate.Input);
        }
        catch (InputCompilationException ex)
        {
            return new List<string> { $"Step '{stepToValidate.Id}': {ex.Message}" };
        }

        var errorMessages = new List<string>();
        foreach (var (parameterName, inputValue) in compiledInput)
        {
            if (inputValue is InputValue.StepOutputReference reference &&
                !HasEarlierOutput(reference, stepToValidate.Id, allSteps))
            {
                errorMessages.Add($"Step '{stepToValidate.Id}' input '{parameterName}' references '{reference.StepId}.{reference.OutputName}', which is not an output of an earlier step.");
            }
        }

        return errorMessages;
    }

    private static bool HasEarlierOutput(InputValue.StepOutputReference reference, string currentStepId, List<StepConfig> allSteps)
    {
        return allSteps
            .TakeWhile(s => s.Id != currentStepId)
            .Any(s => s.Id == reference.StepId && HasOutput(s.Output, reference.OutputName));
    }

    private static bool HasOutput(List<OutputConfig>? outputConfig, string outputName)
    {
        if (outputConfig != null)
        {
            return outputConfig.Any(o => o.As == outputName);
        }
        else
        {
            // The step declares no output. It has a Required annotation on its output, so we do not
            // report the missing reference here to avoid failing twice on the same issue.
            return true;
        }
    }
}
