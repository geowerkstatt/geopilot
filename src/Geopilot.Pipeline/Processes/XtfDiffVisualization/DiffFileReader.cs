using Geopilot.PipelineCore.Pipeline;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Geopilot.Pipeline.Processes.XtfDiffVisualization;

/// <summary>
/// Reads a diff in the JSON format of the XTF-Diff-Tool (its <c>OutputFileDescription.md</c>), including the
/// locations the tool adds with <c>--locations</c>.
/// </summary>
internal static class DiffFileReader
{
    // Lists and structures become text for the list, so letters like ä stay readable instead of ä.
    private static readonly JsonSerializerOptions CompactValueOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Reads the changes of the diff in the order of the file.
    /// </summary>
    /// <param name="diff">The diff file.</param>
    /// <param name="cancellationToken">Cancels reading the file.</param>
    /// <returns>The changes with their locations.</returns>
    /// <exception cref="InvalidOperationException">The file is not a diff of the XTF-Diff-Tool.</exception>
    public static async Task<IReadOnlyList<DiffEntry>> ReadAsync(IPipelineFile diff, CancellationToken cancellationToken)
    {
        await using var stream = await diff.OpenReadAsync(cancellationToken);
        JsonDocument document;
        try
        {
            // shortcut: holds the whole diff in memory, a streaming reader if huge diffs strain it (#1054 measures them)
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"The diff <{diff.OriginalFileName}> is not valid JSON: {e.Message}", e);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"The diff <{diff.OriginalFileName}> is not a JSON array of changes as the XTF-Diff-Tool writes it.");

            return document.RootElement
                .EnumerateArray()
                .Select((element, index) => ReadEntry(element, index, diff.OriginalFileName))
                .ToList();
        }
    }

    private static DiffEntry ReadEntry(JsonElement element, int index, string fileName)
    {
        var position = $"Change {index + 1} of the diff <{fileName}>";
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{position} is not a JSON object.");

        var change = new DiffChange
        {
            Id = $"c{index}",
            ObjectId = ReadRequiredText(element, "oid", position),
            ChangeType = ReadRequiredText(element, "changeType", position) switch
            {
                "added" => DiffChangeType.Added,
                "deleted" => DiffChangeType.Deleted,
                "changed" => DiffChangeType.Changed,
                var unknown => throw new InvalidOperationException($"{position} has the unknown changeType <{unknown}>."),
            },
            ValueType = ReadRequiredText(element, "valueType", position) switch
            {
                "object" => DiffValueType.Object,
                "reference" => DiffValueType.Reference,
                "attribute" => DiffValueType.Attribute,
                "geometry" => DiffValueType.Geometry,
                var unknown => throw new InvalidOperationException($"{position} has the unknown valueType <{unknown}>."),
            },
            InterlisName = ReadRequiredText(element, "interlisName", position),
            AttributePath = ReadOptionalText(element, "attributePath", position),
            OldValue = ReadValue(element, "oldValue"),
            NewValue = ReadValue(element, "newValue"),
        };

        return new DiffEntry(change, ReadOptionalText(element, "oldLocation", position), ReadOptionalText(element, "newLocation", position));
    }

    private static string ReadRequiredText(JsonElement element, string property, string position) =>
        ReadOptionalText(element, property, position) is { Length: > 0 } text
            ? text
            : throw new InvalidOperationException($"{position} has no {property}.");

    private static string? ReadOptionalText(JsonElement element, string property, string position)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new InvalidOperationException($"{position} has a {property} that is not text.");
    }

    private static string? ReadValue(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => JsonSerializer.Serialize(value, CompactValueOptions),
        };
    }
}

/// <summary>
/// A change of the diff with its location in the old and the new state, where the diff has one.
/// </summary>
/// <param name="Change">The change as the list shows it.</param>
/// <param name="OldLocation">The location in the old state as WKT, or <see langword="null"/> without one.</param>
/// <param name="NewLocation">The location in the new state as WKT, or <see langword="null"/> without one.</param>
internal sealed record DiffEntry(DiffChange Change, string? OldLocation, string? NewLocation);
