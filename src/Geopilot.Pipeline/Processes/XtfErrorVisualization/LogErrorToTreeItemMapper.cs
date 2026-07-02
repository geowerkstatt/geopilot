using Geopilot.Pipeline.Visualization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Maps parsed XTF validator log entries to the flat <see cref="TreeItem"/> list of the tree visualization.
/// Each error or warning becomes one item displayed by its object TID (or its message when no TID is present),
/// carrying the error category, model, topic, class, message, line and coordinate as metadata. The frontend
/// groups, counts and renders these items; this mapper does not build any hierarchy.
/// </summary>
internal static class LogErrorToTreeItemMapper
{
    private const string SeverityError = "error";
    private const string SeverityWarning = "warning";

    // A qualified INTERLIS name with at least three segments (Model.Topic.Class...), used to recover the failing
    // object's class from the message when the entry carries no object tag (e.g. constraint or association errors).
    private static readonly Regex QualifiedNamePattern = new(@"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*){2,}", RegexOptions.Compiled);

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
                Severity = isError ? SeverityError : SeverityWarning,
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

        var qualifiedName = QualifiedClassName(logEntry);
        if (qualifiedName is not null)
        {
            metadata["Model"] = qualifiedName[0];
            metadata["Topic"] = qualifiedName[1];
            metadata["Class"] = qualifiedName[2];
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

    /// <summary>
    /// Resolves the failing object's model, topic and class. Prefers the object tag; when that carries no
    /// qualified name, falls back to the first qualified name in the message (e.g. a constraint or association
    /// name such as <c>Model.Topic.Class.Constraint</c>), so those errors still group by class instead of landing
    /// in the ungrouped bucket.
    /// </summary>
    /// <param name="logEntry">The log entry.</param>
    /// <returns>The model, topic and class, or <see langword="null"/> when none can be determined.</returns>
    private static string[]? QualifiedClassName(LogError logEntry)
    {
        var fromObjTag = FirstThreeSegments(logEntry.ObjTag);
        if (fromObjTag is not null)
            return fromObjTag;

        var match = QualifiedNamePattern.Match(logEntry.Message!);
        return match.Success ? FirstThreeSegments(match.Value) : null;
    }

    /// <summary>
    /// Splits a qualified name on dots and returns its first three segments, or <see langword="null"/> when it has
    /// fewer than three.
    /// </summary>
    private static string[]? FirstThreeSegments(string? qualifiedName)
    {
        if (string.IsNullOrEmpty(qualifiedName))
            return null;

        var parts = qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[..3] : null;
    }
}
