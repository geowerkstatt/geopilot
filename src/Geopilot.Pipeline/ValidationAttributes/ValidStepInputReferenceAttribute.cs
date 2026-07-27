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
            foreach (var reference in StepOutputReferencesOf(inputValue))
            {
                if (!HasEarlierStep(reference, stepToValidate.Id, allSteps))
                {
                    errorMessages.Add($"Step '{stepToValidate.Id}' input '{parameterName}' references '{reference.StepId}.{reference.OutputName}', but '{reference.StepId}' is not an earlier step.");
                }
            }
        }

        return errorMessages;
    }

    private static IEnumerable<InputValue.StepOutputReference> StepOutputReferencesOf(InputValue value) => value switch
    {
        InputValue.StepOutputReference reference => new[] { reference },
        InputValue.Sequence sequence => sequence.Items.OfType<InputValue.StepOutputReference>(),
        _ => Enumerable.Empty<InputValue.StepOutputReference>(),
    };

    private static bool HasEarlierStep(InputValue.StepOutputReference reference, string currentStepId, List<StepConfig> allSteps)
    {
        // Outputs are implicit now (every public result property is available), so we only
        // validate that the referenced step exists and runs before the current one. Whether
        // the referenced property actually exists on the process result type is checked closer
        // to where the process type is resolved.
        return allSteps
            .TakeWhile(s => s.Id != currentStepId)
            .Any(s => s.Id == reference.StepId);
    }
}
