using Geopilot.Pipeline.Config;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Validation;

/// <summary>
/// Requires a reaction set to hold at least one reaction, a transfer or a notification. A set without any would let a
/// mandate choose it and then do nothing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal sealed class AtLeastOneReactionAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not ReactionSetConfig set)
            return new ValidationResult("validation object is not of type ReactionSetConfig");

        return set.AllReactions().Any()
            ? ValidationResult.Success
            : new ValidationResult("A reaction set needs at least one transfer or notification.");
    }
}
