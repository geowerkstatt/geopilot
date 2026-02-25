using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Pipeline.Config;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
internal sealed class DuplicatedPropertyAttribute : ValidationAttribute
{
    public string? PropertyName { get; set; }

    public override bool IsValid(object? value)
    {
        if (value is not IEnumerable<object> collectionWithIds)
        {
            return false;
        }

        if (PropertyName == null)
        {
            ErrorMessage = "PropertyName is required for DuplicatedPropertyAttribute.";
            return false;
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
            ErrorMessage = $"Duplicate {PropertyName} found: {string.Join(", ", duplicatedIds)}.";
            return false;
        }
        else
        {
            return true;
        }
    }
}
