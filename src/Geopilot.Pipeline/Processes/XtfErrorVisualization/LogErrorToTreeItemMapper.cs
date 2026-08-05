using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Maps parsed XTF validator log entries to the flat <see cref="TreeItem"/> list of the tree visualization.
/// Each error or warning becomes one item with explicit fields: the classified error category, the TID of the
/// failing object, its model, topic and class, the message, and the line and coordinates when present. The
/// frontend groups, counts and renders these items; this mapper does not build any hierarchy.
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
            var qualifiedName = QualifiedClassName(logEntry);
            items.Add(new TreeItem
            {
                Id = indexedError.Id,
                Severity = isError ? SeverityError : SeverityWarning,
                ErrorType = ErrorTypeClassifier.Classify(logEntry.Message!),
                Tid = string.IsNullOrEmpty(logEntry.Tid) ? null : logEntry.Tid,
                Model = qualifiedName?[0],
                Topic = qualifiedName?[1],
                Class = qualifiedName?[2],
                Message = logEntry.Message!,
                Line = logEntry.Line,
                Coordinates = FormatCoordinates(logEntry.Geometry?.Coord),
            });
        }

        return items;
    }

    /// <summary>
    /// Formats the coordinates as an invariant <c>"C1, C2"</c> display string, or <see langword="null"/>
    /// when there is no coordinate.
    /// </summary>
    private static string? FormatCoordinates(Coord? coord)
    {
        if (coord is null)
            return null;

        return $"{coord.C1.ToString(CultureInfo.InvariantCulture)}, {coord.C2.ToString(CultureInfo.InvariantCulture)}";
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
