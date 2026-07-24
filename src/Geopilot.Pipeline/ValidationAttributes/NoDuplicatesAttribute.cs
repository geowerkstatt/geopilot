using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.ValidationAttributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
internal sealed class NoDuplicatesAttribute : ValidationAttribute
{
    public string? PropertyName { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // An absent (null) collection has no duplicates. This matters for optional collections such
        // as StepConfig.Output, which is null when a step declares no output_actions.
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not IEnumerable<object> collectionWithIds)
        {
            return new ValidationResult("validation object is not of type IEnumerable<object>");
        }

        if (PropertyName == null)
        {
            return new ValidationResult("PropertyName is required for NoDuplicatesAttribute.");
        }

        var duplicatedIds = collectionWithIds
            .GroupBy(obj =>
            {
                var objType = obj.GetType();
                if (objType != null)
                {
                    var idProperty = objType.GetProperty(PropertyName);
                    if (idProperty != null)
                    {
                        return idProperty.GetValue(obj, null);
                    }
                }

                return null;
            })
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        if (duplicatedIds.Any())
        {
            return new ValidationResult($"Duplicate {PropertyName} found: {string.Join(", ", duplicatedIds)}.");
        }
        else
        {
            return ValidationResult.Success;
        }
    }
}
