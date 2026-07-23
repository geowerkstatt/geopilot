using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.ValidationAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class UniqueStatusMessageOutputAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not StepConfig step)
        {
            return new ValidationResult("Validation object is not of type StepConfig.");
        }

        var outputs = step.Output;
        if (outputs is null || outputs.Count == 0)
        {
            return ValidationResult.Success;
        }

        var statusMessageCount = outputs.Count(o => o.Actions?.Contains(OutputAction.StatusMessage) == true);
        if (statusMessageCount > 1)
        {
            return new ValidationResult(
                $"Step '{step.Id}' has multiple outputs with StatusMessage action. Only one StatusMessage output is allowed per step.");
        }

        return ValidationResult.Success;
    }
}
