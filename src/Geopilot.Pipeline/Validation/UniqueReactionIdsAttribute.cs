using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Validation;

/// <summary>
/// Requires reaction ids to be unique across all <c>reaction_sets</c>, not only within one set. A mandate that later
/// chooses several sets then never gets two reactions with the same id.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class UniqueReactionIdsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not IEnumerable<ReactionSetConfig> sets)
            return new ValidationResult("validation object is not of type IEnumerable<ReactionSetConfig>");

        var duplicates = sets
            .SelectMany(set => set.Transfers ?? [])
            .GroupBy(reaction => reaction.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"The reaction id '{group.Key}' is used more than once.")
            .ToList();

        return duplicates.Count == 0
            ? ValidationResult.Success
            : new ValidationResult($"{string.Join(" ", duplicates)} Reaction ids must be unique across all reaction sets.");
    }
}
