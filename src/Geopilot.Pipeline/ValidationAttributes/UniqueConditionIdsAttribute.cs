using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.ValidationAttributes;

/// <summary>
/// A condition id must be unique within its step, across all condition lists of the step: two conditions
/// of the same step sharing an id would be indistinguishable for a consumer of their evaluation results.
/// Reusing an id on other steps or pipelines stays allowed on purpose, it marks the same rule. Conditions
/// without id are not checked.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class UniqueConditionIdsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not StepConfig step)
        {
            return new ValidationResult("Validation object is not of type StepConfig.");
        }

        var conditionLists = new[]
        {
            step.Conditions?.Pre?.FailConditions,
            step.Conditions?.Pre?.SkipConditions,
            step.Conditions?.Post?.FailConditions,
            step.Conditions?.Post?.RestrictDeliveryConditions,
            step.Conditions?.Post?.WarnConditions,
        };

        var duplicatedIds = conditionLists
            .OfType<List<ConditionConfig>>()
            .SelectMany(conditions => conditions)
            .Select(condition => condition.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicatedIds.Count > 0)
        {
            return new ValidationResult(
                $"Step '{step.Id}' has duplicate condition ids: {string.Join(", ", duplicatedIds)}. A condition id must be unique within its step.");
        }

        return ValidationResult.Success;
    }
}
