using Geopilot.Api.Models;
using Geopilot.Pipeline;
using System.Globalization;
using System.Text.Json;
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

    /// <summary>
    /// Checks delivered values against the fields of a mandate. The snapshot holds every field in the mandate's order
    /// with its normalized value (null when not filled in), also when there are errors; callers use it only without errors.
    /// Errors are keyed <c>metadata.&lt;id&gt;</c>.
    /// </summary>
    public static MetadataValueCheck CheckValues(IReadOnlyList<MetadataField> fields, IReadOnlyDictionary<string, JsonElement>? values)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var errors = new Dictionary<string, string[]>();
        var snapshot = new List<DeliveredMetadataField>(fields.Count);

        foreach (var field in fields)
        {
            JsonElement? value = null;
            if (values is not null && values.TryGetValue(field.Id, out var raw))
            {
                var normalized = Normalize(field, raw, out var problem);
                if (problem is not null)
                    errors[$"metadata.{field.Id}"] = [problem];
                else
                    value = normalized;
            }

            if (value is null && field.Evaluation == FieldEvaluationType.Required && !errors.ContainsKey($"metadata.{field.Id}"))
                errors[$"metadata.{field.Id}"] = [$"The metadata field <{field.Id}> is required for this mandate."];

            snapshot.Add(new DeliveredMetadataField(field.Id, field.Title, field.Type, field.Evaluation, field.Options, value));
        }

        var knownIds = fields.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var key in values?.Keys ?? Enumerable.Empty<string>())
        {
            if (!knownIds.Contains(key))
                errors[$"metadata.{key}"] = [$"The metadata field <{key}> is not defined for this mandate."];
        }

        return new MetadataValueCheck(snapshot, errors);
    }

    // Returns null with no problem when the value counts as not filled in.
    private static JsonElement? Normalize(MetadataField field, JsonElement raw, out string? problem)
    {
        problem = null;
        var wrongType = $"The value of the metadata field <{field.Id}> is not a valid {field.Type}.";

        if (raw.ValueKind == JsonValueKind.Null || (raw.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(raw.GetString())))
            return null;

        switch (field.Type)
        {
            case MetadataFieldType.Text:
                return NormalizedString(raw, out problem, wrongType, text => text);
            case MetadataFieldType.TextList:
                return NormalizeTextList(raw, out problem, wrongType);
            case MetadataFieldType.Selection:
                return NormalizedString(
                    raw,
                    out problem,
                    $"The value of the metadata field <{field.Id}> is not one of its options.",
                    text => (field.Options ?? []).Any(option => option.Id == text) ? text : null);
            case MetadataFieldType.WholeNumber:
                if (raw.ValueKind == JsonValueKind.Number && raw.TryGetInt64(out var whole))
                    return JsonSerializer.SerializeToElement(whole);
                break;
            case MetadataFieldType.DecimalNumber:
                if (raw.ValueKind == JsonValueKind.Number && raw.TryGetDecimal(out var number))
                    return JsonSerializer.SerializeToElement(number);
                break;
            case MetadataFieldType.Date:
                return NormalizedString(
                    raw,
                    out problem,
                    $"The value of the metadata field <{field.Id}> is not a date in the form yyyy-MM-dd.",
                    text => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? text : null);
        }

        problem = wrongType;
        return null;
    }

    // A blank string is not filled in; accept maps the trimmed text to the stored text or null when it is invalid.
    private static JsonElement? NormalizedString(JsonElement raw, out string? problem, string invalid, Func<string, string?> accept)
    {
        problem = null;
        if (raw.ValueKind != JsonValueKind.String)
        {
            problem = invalid;
            return null;
        }

        var text = raw.GetString()!.Trim();
        if (text.Length == 0)
            return null;

        var stored = accept(text);
        if (stored is null)
        {
            problem = invalid;
            return null;
        }

        return JsonSerializer.SerializeToElement(stored);
    }

    private static JsonElement? NormalizeTextList(JsonElement raw, out string? problem, string invalid)
    {
        problem = null;
        if (raw.ValueKind != JsonValueKind.Array || raw.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            problem = invalid;
            return null;
        }

        var entries = raw.EnumerateArray().Select(item => item.GetString()!.Trim()).Where(text => text.Length > 0).ToArray();
        return entries.Length == 0 ? null : JsonSerializer.SerializeToElement(entries);
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

/// <summary>
/// The outcome of checking delivered metadata values: the copy a delivery keeps and the errors keyed by field.
/// </summary>
public sealed record MetadataValueCheck(IReadOnlyList<DeliveredMetadataField> Snapshot, IReadOnlyDictionary<string, string[]> Errors);
