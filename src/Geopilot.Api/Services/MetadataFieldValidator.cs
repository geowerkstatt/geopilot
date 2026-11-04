using Geopilot.Api.Models;
using Geopilot.Pipeline;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Services;

/// <summary>
/// Checks the additional metadata fields an administrator defines for a mandate.
/// </summary>
public static partial class MetadataFieldValidator
{
    /// <summary>
    /// The problems of the definitions, one English message each, naming the field by position (1-based) and id;
    /// empty when they can be saved.
    /// </summary>
    public static IReadOnlyList<string> ValidateDefinitions(IReadOnlyList<MetadataField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var problems = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenTitles = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            var label = $"Metadata field {index + 1} <{field.Id}>";

            if (!IdPattern().IsMatch(field.Id ?? string.Empty))
                problems.Add($"{label}: The id must start with a lowercase letter followed by up to 63 lowercase letters, digits or underscores.");
            else if (!seenIds.Add(field.Id!))
                problems.Add($"{label}: The id is used by another field.");

            var displayTitle = field.Title?.GetDisplayText() ?? string.Empty;
            if (displayTitle.Length == 0)
                problems.Add($"{label}: The title needs text in at least one language.");
            else if (!seenTitles.Add(displayTitle))
                problems.Add($"{label}: The title is used by another field.");

            if (!Enum.IsDefined(field.Type))
                problems.Add($"{label}: The type is not known.");

            if (field.Evaluation is not (FieldEvaluationType.Optional or FieldEvaluationType.Required))
                problems.Add($"{label}: The evaluation must be optional or required.");

            problems.AddRange(ValidateOptions(field, label));
        }

        return problems;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,63}\z")]
    private static partial Regex IdPattern();

    private static IEnumerable<string> ValidateOptions(MetadataField field, string label)
    {
        var options = field.Options ?? [];

        if (field.Type != MetadataFieldType.Selection)
        {
            if (options.Count > 0)
                yield return $"{label}: Only a selection field takes options.";
            yield break;
        }

        if (options.Count == 0)
            yield return $"{label}: A selection needs at least one option.";

        var seenOptionIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var optionLabel = $"{label}, option {index + 1} <{option.Id}>";

            if (!IdPattern().IsMatch(option.Id ?? string.Empty))
                yield return $"{optionLabel}: The id must start with a lowercase letter followed by up to 63 lowercase letters, digits or underscores.";
            else if (!seenOptionIds.Add(option.Id!))
                yield return $"{optionLabel}: The id is used by another option of this field.";

            if (option.Title is null || option.Title.GetDisplayText().Length == 0)
                yield return $"{optionLabel}: The title needs text in at least one language.";
        }
    }
}
