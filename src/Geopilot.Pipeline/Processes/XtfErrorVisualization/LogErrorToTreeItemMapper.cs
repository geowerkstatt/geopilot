using Geopilot.Pipeline.Visualization;
using System.Globalization;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Maps parsed XTF validator log entries to the flat <see cref="TreeItem"/> list of the tree visualization.
/// Each error or warning becomes one item displayed by its object TID (or its message when no TID is present),
/// carrying the error category, model, topic, class, message, line and coordinate as metadata. The frontend
/// groups, counts and renders these items; this mapper does not build any hierarchy.
/// </summary>
internal static class LogErrorToTreeItemMapper
{
    private const string IconError = "error_outline";
    private const string IconWarning = "warning_amber";

    private const string ColorError = "error";
    private const string ColorWarning = "warning";

    /// <summary>
    /// Maps the warnings and errors of the given log entries to flat tree items, skipping informational entries
    /// and entries without a message.
    /// </summary>
    /// <param name="logEntries">The parsed validator log entries.</param>
    /// <returns>The flat tree items for the frontend tree visualization.</returns>
    public static IReadOnlyList<TreeItem> Map(IReadOnlyList<IndexedError> logEntries)
    {
        ArgumentNullException.ThrowIfNull(logEntries);

        var items = new List<TreeItem>();
        foreach (var indexedError in logEntries)
        {
            var logEntry = indexedError.Error;
            if (string.IsNullOrEmpty(logEntry.Message))
                continue;
            if (!Enum.TryParse(logEntry.Type, out LogEntryType severity) || severity == LogEntryType.Info)
                continue;

            var isError = severity == LogEntryType.Error;
            items.Add(new TreeItem
            {
                Id = indexedError.Id,
                Label = string.IsNullOrEmpty(logEntry.Tid) ? logEntry.Message! : logEntry.Tid!,
                Icon = isError ? IconError : IconWarning,
                Color = isError ? ColorError : ColorWarning,
                Metadata = BuildMetadata(logEntry),
            });
        }

        return items;
    }

    /// <summary>
    /// Builds the metadata of a log entry in a stable display order: the error category, the TID of the failing
    /// object, model, topic and class from the object tag, the full message, then the line and coordinates when present.
    /// </summary>
    /// <param name="logEntry">The log entry whose details are collected.</param>
    /// <returns>The metadata of the tree item.</returns>
    private static Dictionary<string, object> BuildMetadata(LogError logEntry)
    {
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal);

        var category = ErrorTypeClassifier.Classify(logEntry.Message!);
        if (category is not null)
            metadata["Error type"] = category;

        if (!string.IsNullOrEmpty(logEntry.Tid))
            metadata["TID"] = logEntry.Tid;

        var objTagParts = string.IsNullOrEmpty(logEntry.ObjTag)
            ? Array.Empty<string>()
            : logEntry.ObjTag.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (objTagParts.Length == 3)
        {
            metadata["Model"] = objTagParts[0];
            metadata["Topic"] = objTagParts[1];
            metadata["Class"] = objTagParts[2];
        }

        metadata["Message"] = logEntry.Message!;

        if (logEntry.Line.HasValue)
            metadata["Line"] = logEntry.Line.Value.ToString(CultureInfo.InvariantCulture);

        var coord = logEntry.Geometry?.Coord;
        if (coord is not null)
        {
            metadata["Coordinates"] =
                $"{coord.C1.ToString(CultureInfo.InvariantCulture)}, {coord.C2.ToString(CultureInfo.InvariantCulture)}";
        }

        return metadata;
    }
}
