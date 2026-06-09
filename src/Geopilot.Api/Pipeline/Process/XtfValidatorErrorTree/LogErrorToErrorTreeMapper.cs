using Geopilot.Api.Pipeline.Process.TreeVisualization;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Builds a generic <see cref="TreeNode"/> hierarchy from XTF validator log entries.
/// The hierarchy is organized as model, topic, class and a leaf per constraint or message.
/// </summary>
public class LogErrorToErrorTreeMapper
{
    private const string IconError = "error_outline";
    private const string IconWarning = "warning_amber";
    private const string IconSuccess = "check_circle_outline";

    private const string ColorError = "error";
    private const string ColorWarning = "warning";
    private const string ColorSuccess = "success";

    /// <summary>
    /// A single leaf of the tree together with the model, topic and class it belongs to and its severity.
    /// </summary>
    private sealed record LeafEntry(string Model, string Topic, string Class, string Leaf, LogEntryType Severity);

    /// <summary>
    /// Patterns that match the info log entries which announce the validation of a constraint.
    /// The captured group is the qualified constraint name "Model.Topic.Class.Constraint".
    /// </summary>
    private static readonly Regex[] ConstraintPatterns =
    {
        new Regex(@"^validate mandatory constraint (\S+)\.\.\.$"),
        new Regex(@"^validate plausibility constraint (\S+)\.\.\.$"),
        new Regex(@"^validate existence constraint (\S+)\.\.\.$"),
        new Regex(@"^validate unique constraint (\S+)\.\.\.$"),
        new Regex(@"^validate set constraint (\S+)\.\.\.$"),
    };

    /// <summary>
    /// Matches an embedded qualified constraint name "Model.Topic.Class.Constraint" with its optional
    /// trailing INTERLIS syntax in parentheses. Used to remove that redundant part from a message for display,
    /// since the class is already represented by the surrounding tree nodes.
    /// </summary>
    private static readonly Regex QualifiedNamePattern = new Regex(@"\w+\.\w+\.\w+\.\w+(\s*\([^)]*\))?");

    private static readonly Regex WhitespacePattern = new Regex(@"\s+");

    private readonly List<LeafEntry> leafEntries = new List<LeafEntry>();
    private readonly List<string> otherErrorMessages = new List<string>();
    private readonly List<string> otherWarningMessages = new List<string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogErrorToErrorTreeMapper"/> class using the specified collection of log entries.
    /// </summary>
    /// <remarks>The constructor processes the provided log entries to collect validated constraints, warnings and
    /// errors so that <see cref="Map"/> can build the tree from them.</remarks>
    /// <param name="logEntries">The collection of log entries to be processed for building the log hierarchy. Cannot be null.</param>
    public LogErrorToErrorTreeMapper(IEnumerable<LogError> logEntries)
    {
        var logEntryList = logEntries.ToList();
        CollectConstraintInfos(logEntryList);
        CollectWarningsAndErrors(logEntryList);
    }

    /// <summary>
    /// Converts the collected log entries into a <see cref="TreeNode"/> hierarchy.
    /// Model, topic and class nodes carry the severity of their most severe descendant, leaf nodes carry their own severity.
    /// </summary>
    /// <returns>The tree nodes ready for the frontend tree visualization.</returns>
    public IReadOnlyList<TreeNode> Map()
    {
        var rootNodes = new List<TreeNode>();

        foreach (var modelGroup in leafEntries.GroupBy(e => e.Model, StringComparer.Ordinal))
        {
            var topicNodes = new List<TreeNode>();
            foreach (var topicGroup in modelGroup.GroupBy(e => e.Topic, StringComparer.Ordinal))
            {
                var classNodes = new List<TreeNode>();
                foreach (var classGroup in topicGroup.GroupBy(e => e.Class, StringComparer.Ordinal))
                {
                    var leaves = classGroup
                        .Select(e => CreateNode(e.Leaf, e.Severity, new List<TreeNode>()))
                        .ToList<TreeNode>();
                    classNodes.Add(CreateNode(classGroup.Key, ReduceSeverity(classGroup), leaves));
                }

                topicNodes.Add(CreateNode(topicGroup.Key, ReduceSeverity(topicGroup), classNodes));
            }

            rootNodes.Add(CreateNode(modelGroup.Key, ReduceSeverity(modelGroup), topicNodes));
        }

        if (otherErrorMessages.Count > 0 || otherWarningMessages.Count > 0)
        {
            var errors = otherErrorMessages.Select(message => CreateNode(message, LogEntryType.Error, new List<TreeNode>()));
            var warnings = otherWarningMessages.Select(message => CreateNode(message, LogEntryType.Warning, new List<TreeNode>()));
            var groupSeverity = otherErrorMessages.Count > 0 ? LogEntryType.Error : LogEntryType.Warning;
            rootNodes.Add(CreateNode("Other Messages", groupSeverity, errors.Concat(warnings).ToList<TreeNode>()));
        }

        return rootNodes;
    }

    /// <summary>
    /// Creates a tree node with the icon and color that match the given severity.
    /// </summary>
    private static TreeNode CreateNode(string message, LogEntryType severity, IList<TreeNode> children) => new()
    {
        Message = message,
        Icon = severity switch
        {
            LogEntryType.Error => IconError,
            LogEntryType.Warning => IconWarning,
            _ => IconSuccess,
        },
        Color = severity switch
        {
            LogEntryType.Error => ColorError,
            LogEntryType.Warning => ColorWarning,
            _ => ColorSuccess,
        },
        Values = children,
    };

    /// <summary>
    /// Collects all validated constraints from the info log entries as success leaves.
    /// </summary>
    /// <param name="logEntries">Entries of the validator log.</param>
    private void CollectConstraintInfos(IEnumerable<LogError> logEntries)
    {
        foreach (var logEntry in logEntries)
        {
            if (string.IsNullOrEmpty(logEntry.Message))
                continue;
            if (!Enum.TryParse(logEntry.Type, out LogEntryType logEntryType) || logEntryType != LogEntryType.Info)
                continue;

            foreach (var pattern in ConstraintPatterns)
            {
                var match = pattern.Match(logEntry.Message);
                if (match.Success)
                {
                    var parts = SplitQualifiedName(match.Groups[1].Value);
                    if (parts.Length == 4)
                        leafEntries.Add(new LeafEntry(parts[0], parts[1], parts[2], parts[3], LogEntryType.Info));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Collects warnings and errors as leaves, using the object tag to determine model, topic and class.
    /// Entries without a qualified object tag are collected as ungrouped other messages.
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

            var objTagParts = string.IsNullOrEmpty(logEntry.ObjTag)
                ? Array.Empty<string>()
                : SplitQualifiedName(logEntry.ObjTag);

            if (objTagParts.Length == 3)
            {
                var leaf = TrimQualifiedName(logEntry.Message);
                leafEntries.Add(new LeafEntry(objTagParts[0], objTagParts[1], objTagParts[2], leaf, logEntryType));
            }
            else if (logEntryType == LogEntryType.Error)
            {
                otherErrorMessages.Add(logEntry.Message);
            }
            else
            {
                otherWarningMessages.Add(logEntry.Message);
            }
        }
    }

    /// <summary>
    /// Removes an embedded qualified constraint name and its INTERLIS syntax from a message and normalizes whitespace.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <returns>The message without the redundant qualified name.</returns>
    private static string TrimQualifiedName(string message)
    {
        var withoutName = QualifiedNamePattern.Replace(message, string.Empty);
        return WhitespacePattern.Replace(withoutName, " ").Trim();
    }

    /// <summary>
    /// Splits a qualified name on dots, ignoring empty segments.
    /// </summary>
    /// <param name="qualifiedName">The qualified name, e.g. "Model.Topic.Class".</param>
    /// <returns>The name parts.</returns>
    private static string[] SplitQualifiedName(string qualifiedName) =>
        qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Reduces the severities of the given leaf entries to the most severe one (Error before Warning before Info).
    /// </summary>
    private static LogEntryType ReduceSeverity(IEnumerable<LeafEntry> entries) =>
        entries.Aggregate(LogEntryType.Info, (severity, entry) => ReduceType(severity, entry.Severity));

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
