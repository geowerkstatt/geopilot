using Geopilot.Pipeline.Processes.TreeVisualization;
using System.Globalization;

namespace Geopilot.Pipeline.Processes.XtfValidatorErrorTree;

/// <summary>
/// Builds a generic <see cref="TreeNode"/> hierarchy from XTF validator log entries.
/// The tree groups errors and warnings by their error type. Each group lists its individual
/// occurrences as leaves, displayed by the object TID, with the model, topic, class and full
/// message carried as metadata. Entries without a TID are collected under a single trailing group.
/// </summary>
public class LogErrorToErrorTreeMapper
{
    private const string IconError = "error_outline";
    private const string IconWarning = "warning_amber";

    private const string ColorError = "error";
    private const string ColorWarning = "warning";

    private const string OtherGroupName = "Other Errors/Warnings";

    /// <summary>
    /// A single occurrence of an error or warning, with the text shown for it, its severity and its metadata.
    /// </summary>
    private sealed record Occurrence(string Display, LogEntryType Severity, IDictionary<string, string> Metadata);

    private readonly Dictionary<string, List<Occurrence>> occurrencesByCategory = new(StringComparer.Ordinal);
    private readonly List<Occurrence> otherOccurrences = new List<Occurrence>();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogErrorToErrorTreeMapper"/> class using the specified collection of log entries.
    /// </summary>
    /// <remarks>The constructor collects the errors and warnings so that <see cref="Map"/> can build the tree from them.</remarks>
    /// <param name="logEntries">The collection of log entries to be processed for building the tree. Cannot be null.</param>
    public LogErrorToErrorTreeMapper(IEnumerable<LogError> logEntries)
    {
        ArgumentNullException.ThrowIfNull(logEntries);
        CollectWarningsAndErrors(logEntries);
    }

    /// <summary>
    /// Converts the collected errors and warnings into a <see cref="TreeNode"/> hierarchy.
    /// Group nodes carry the severity of their most severe occurrence, leaf nodes carry their own severity.
    /// Groups are ordered by severity, then by occurrence count descending, then by category name.
    /// The group of entries without a TID is appended last.
    /// </summary>
    /// <returns>The tree nodes ready for the frontend tree visualization.</returns>
    public IReadOnlyList<TreeNode> Map()
    {
        var rootNodes = occurrencesByCategory
            .OrderByDescending(group => ReduceSeverity(group.Value))
            .ThenByDescending(group => group.Value.Count)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => CreateGroup(group.Key, group.Value))
            .ToList();

        if (otherOccurrences.Count > 0)
            rootNodes.Add(CreateGroup(OtherGroupName, otherOccurrences));

        return rootNodes;
    }

    /// <summary>
    /// Creates a group node with its occurrence leaves, carrying the severity of its most severe occurrence.
    /// </summary>
    private static TreeNode CreateGroup(string name, IReadOnlyList<Occurrence> occurrences)
    {
        var leaves = occurrences
            .Select(occurrence => CreateNode(occurrence.Display, occurrence.Severity, new List<TreeNode>(), occurrence.Metadata))
            .ToList<TreeNode>();

        return CreateNode(name, ReduceSeverity(occurrences), leaves);
    }

    /// <summary>
    /// Creates a tree node with the icon and color that match the given severity and the optional metadata details.
    /// </summary>
    private static TreeNode CreateNode(string message, LogEntryType severity, IList<TreeNode> children, IDictionary<string, string>? metadata = null) => new()
    {
        Message = message,
        Icon = severity == LogEntryType.Error ? IconError : IconWarning,
        Color = severity == LogEntryType.Error ? ColorError : ColorWarning,
        Values = children,
        Metadata = metadata,
    };

    /// <summary>
    /// Collects warnings and errors as occurrences. An entry with a TID whose message matches a known error type
    /// is grouped under that type and displayed by its TID. Any other entry is collected under the other group
    /// and displayed by its full message.
    /// </summary>
    /// <param name="logEntries">Entries of the validator log.</param>
    private void CollectWarningsAndErrors(IEnumerable<LogError> logEntries)
    {
        foreach (var logEntry in logEntries)
        {
            if (string.IsNullOrEmpty(logEntry.Message))
                continue;
            if (!Enum.TryParse(logEntry.Type, out LogEntryType logEntryType) || logEntryType == LogEntryType.Info)
                continue;

            var metadata = BuildMetadata(logEntry);
            var category = string.IsNullOrEmpty(logEntry.Tid) ? null : ErrorTypeClassifier.Classify(logEntry.Message);

            if (category is not null)
            {
                var occurrences = occurrencesByCategory.TryGetValue(category, out var existing)
                    ? existing
                    : occurrencesByCategory[category] = new List<Occurrence>();
                occurrences.Add(new Occurrence(logEntry.Tid!, logEntryType, metadata));
            }
            else
            {
                otherOccurrences.Add(new Occurrence(logEntry.Message, logEntryType, metadata));
            }
        }
    }

    /// <summary>
    /// Builds the metadata of a log entry, preserving a stable display order: the TID of the failing object,
    /// model, topic and class from the object tag, the full message, then the line and coordinates when present.
    /// </summary>
    /// <param name="logEntry">The log entry whose details are collected.</param>
    /// <returns>The metadata details.</returns>
    private static Dictionary<string, string> BuildMetadata(LogError logEntry)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(logEntry.Tid))
            metadata["TID"] = logEntry.Tid;

        var objTagParts = string.IsNullOrEmpty(logEntry.ObjTag)
            ? Array.Empty<string>()
            : SplitQualifiedName(logEntry.ObjTag);

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

    /// <summary>
    /// Splits a qualified name on dots, ignoring empty segments.
    /// </summary>
    /// <param name="qualifiedName">The qualified name, e.g. "Model.Topic.Class".</param>
    /// <returns>The name parts.</returns>
    private static string[] SplitQualifiedName(string qualifiedName) =>
        qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Reduces the severities of the given occurrences to the most severe one (Error before Warning).
    /// </summary>
    private static LogEntryType ReduceSeverity(IEnumerable<Occurrence> occurrences) =>
        occurrences.Aggregate(LogEntryType.Info, (severity, occurrence) => ReduceType(severity, occurrence.Severity));

    /// <summary>
    /// Reduces the given types (Error, Warning or Info) to the type with higher priority.
    /// </summary>
    /// <param name="typeA">The first type.</param>
    /// <param name="typeB">The second type.</param>
    /// <returns>The type with higher priority.</returns>
    private static LogEntryType ReduceType(LogEntryType typeA, LogEntryType typeB)
    {
        if (typeA == LogEntryType.Error || typeB == LogEntryType.Error)
        {
            return LogEntryType.Error;
        }
        else if (typeA == LogEntryType.Warning || typeB == LogEntryType.Warning)
        {
            return LogEntryType.Warning;
        }

        return LogEntryType.Info;
    }
}
