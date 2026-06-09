using Geopilot.Api.Pipeline.Process.TreeVisualization;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Builds a generic <see cref="TreeNode"/> hierarchy from XTF validator log entries.
/// </summary>
public class LogErrorToErrorTreeMapper
{
    private const string IconError = "error_outline";
    private const string IconWarning = "warning_amber";
    private const string IconSuccess = "check_circle_outline";

    private const string ColorError = "error";
    private const string ColorWarning = "warning";
    private const string ColorSuccess = "success";

    private class ConstraintEntry
    {
        public required string ConstraintName { get; set; }

        public required LogEntryType LogEntryType { get; set; }

        public ConstraintType? ConstraintType { get; set; }

        public required string Message { get; set; }
    }

    private static readonly (ConstraintType Type, Regex Pattern)[] ConstraintPatterns =
    {
        (ConstraintType.Mandatory, new Regex(@"^validate mandatory constraint (\S+)\.\.\.$")),
        (ConstraintType.Plausibility, new Regex(@"^validate plausibility constraint (\S+)\.\.\.$")),
        (ConstraintType.Existence, new Regex(@"^validate existence constraint (\S+)\.\.\.$")),
        (ConstraintType.Uniqueness, new Regex(@"^validate unique constraint (\S+)\.\.\.$")),
        (ConstraintType.Set, new Regex(@"^validate set constraint (\S+)\.\.\.$")),
    };

    /// <summary>
    /// Pattern to extract the custom error message of a constraint from a log entry.
    /// Requires verbose logging to be enabled that the qualified name and INTERLIS syntax of the constraint is included.
    /// e.g. "Custom message ModelA.TopicA.ClassA.ConstraintName (INTERLIS syntax)" will result in "Custom message".
    /// </summary>
    private static readonly Regex CustomConstraintMessagePattern = new Regex(@"^(.*) (\w+\.\w+\.\w+\.\w+) \(.*\)$");

    /// <summary>
    /// Pattern to detect a message related to a constraint from a log entry.
    /// e.g. " ModelA.TopicA.ClassA.ConstraintName (INTERLIS syntax) " (the INTERLIS syntax is optional and will be removed from the message).
    /// </summary>
    private static readonly Regex ConstraintNamePattern = new Regex(@"\s(\w+\.\w+\.\w+\.\w+)(\s\(.+\))?\s");

    private readonly List<ConstraintEntry> logEntries = new List<ConstraintEntry>();
    private readonly HashSet<string> otherErrorMessages = new HashSet<string>();
    private readonly HashSet<string> otherWarningMessages = new HashSet<string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogErrorToErrorTreeMapper"/> class using the specified collection of log entries.
    /// </summary>
    /// <remarks>The constructor processes the provided log entries to collect constraint information,
    /// warnings, and errors. Ensure that the collection contains valid LogError objects to accurately represent the log
    /// hierarchy.</remarks>
    /// <param name="logEntries">The collection of log entries to be processed for building the log hierarchy. Cannot be null.</param>
    public LogErrorToErrorTreeMapper(IEnumerable<LogError> logEntries)
    {
        var logEntryList = logEntries.ToList();
        CollectConstraintInfos(logEntryList);
        CollectWarningsAndErrors(logEntryList);
    }

    /// <summary>
    /// Converts the collected log entries into a <see cref="TreeNode"/> hierarchy.
    /// Model and class header nodes carry no icon, leaf constraint nodes are tagged with success, warning, or error icons.
    /// </summary>
    /// <returns>The tree nodes ready for the frontend tree visualization.</returns>
    public List<TreeNode> Map()
    {
        var rootNodes = new List<TreeNode>();
        var modelGroups = logEntries.GroupBy(e => GetModelName(e.ConstraintName));

        foreach (var modelGroup in modelGroups)
        {
            var modelName = modelGroup.Key;
            var modelSeverity = LogEntryType.Info;
            var modelChildren = new List<TreeNode>();

            var classGroups = modelGroup.GroupBy(e => GetClassNameOfConstraint(e.ConstraintName));
            foreach (var classGroup in classGroups)
            {
                var fullClassName = classGroup.Key;
                var className = fullClassName.Substring(modelName.Length + 1);
                var classSeverity = classGroup.Aggregate(LogEntryType.Info, (type, c) => ReduceType(type, c.LogEntryType));
                var classChildren = classGroup
                    .Select(e => CreateNode(e.Message.Replace(fullClassName + ".", string.Empty), e.LogEntryType, new List<TreeNode>()))
                    .ToList();
                modelChildren.Add(CreateNode(className, classSeverity, classChildren.Cast<TreeNode>().ToList()));
                modelSeverity = ReduceType(modelSeverity, classSeverity);
            }

            rootNodes.Add(CreateNode(modelName, modelSeverity, modelChildren));
        }

        if (otherErrorMessages.Count > 0 || otherWarningMessages.Count > 0)
        {
            var errors = otherErrorMessages.Select(message => CreateNode(message, LogEntryType.Error, new List<TreeNode>()));
            var warnings = otherWarningMessages.Select(message => CreateNode(message, LogEntryType.Warning, new List<TreeNode>()));
            var groupSeverity = otherErrorMessages.Count > 0 ? LogEntryType.Error : LogEntryType.Warning;
            rootNodes.Add(CreateNode("Other Messages", groupSeverity, errors.Concat(warnings).ToList()));
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
    /// Collects all constraints from the given logEntries without checking their validation results.
    /// </summary>
    /// <param name="logEntries">Entries of the validator log.</param>
    private void CollectConstraintInfos(IList<LogError> logEntries)
    {
        foreach (var logEntry in logEntries)
        {
            if (string.IsNullOrEmpty(logEntry.Message))
                continue;
            if (Enum.TryParse(logEntry.Type, out LogEntryType logEntryType) && logEntryType == LogEntryType.Info)
            {
                foreach (var (constraintType, pattern) in ConstraintPatterns)
                {
                    var constraintMatch = pattern.Match(logEntry.Message);
                    if (constraintMatch.Success)
                    {
                        var constraintName = constraintMatch.Groups[1].Value;
                        AddLogEntry(constraintName, logEntryType, constraintType, constraintName);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Collects warnings and errors of the constraints and other entries from the given logEntries.
    /// </summary>
    /// <param name="logEntries">Entries of the validator log.</param>
    private void CollectWarningsAndErrors(IList<LogError> logEntries)
    {
        foreach (var logEntry in logEntries)
        {
            if (string.IsNullOrEmpty(logEntry.Message))
                continue;
            if (Enum.TryParse(logEntry.Type, out LogEntryType logEntryType) && logEntryType != LogEntryType.Info)
            {
                var customMessageMatch = CustomConstraintMessagePattern.Match(logEntry.Message);
                if (customMessageMatch.Success)
                {
                    var customMessage = customMessageMatch.Groups[1].Value;
                    var constraintName = customMessageMatch.Groups[2].Value;
                    AddLogEntry(constraintName, logEntryType, customMessage);
                }
                else
                {
                    var nameMatch = ConstraintNamePattern.Match(logEntry.Message);
                    if (nameMatch.Success)
                    {
                        var constraintName = nameMatch.Groups[1].Value;
                        var interlisSyntax = nameMatch.Groups[2].Success ? nameMatch.Groups[2].Value : null;
                        var logMessage = interlisSyntax != null ? logEntry.Message.Replace(interlisSyntax, string.Empty) : logEntry.Message;
                        AddLogEntry(constraintName, logEntryType, logMessage);
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
        }
    }

    /// <summary>
    /// Updates the log entry of the constraint with the given name.
    /// </summary>
    /// <param name="constraintName">The qualified name of the constraint.</param>
    /// <param name="logEntryType">The log entry type.</param>
    /// <param name="message">The log message.</param>
    private void AddLogEntry(string constraintName, LogEntryType logEntryType, string message)
    {
        AddLogEntry(constraintName, logEntryType, null, message);
    }

    /// <summary>
    /// Updates the log entry of the constraint with the given name.
    /// </summary>
    /// <param name="constraintName">The qualified name of the constraint.</param>
    /// <param name="logEntryType">The log entry type.</param>
    /// <param name="constraintType">The type of the constraint.</param>
    /// <param name="message">The log message.</param>
    private void AddLogEntry(string constraintName, LogEntryType logEntryType, ConstraintType? constraintType, string message)
    {
        logEntries.Add(new ConstraintEntry
        {
            ConstraintName = constraintName,
            LogEntryType = logEntryType,
            ConstraintType = constraintType,
            Message = message,
        });
    }

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

    /// <summary>
    /// Gets the model name of the given qualified constraint name.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the constraint.</param>
    /// <returns>The model name of the constraint.</returns>
    private static string GetModelName(string qualifiedName)
    {
        var index = qualifiedName.IndexOf('.');
        return index == -1 ? qualifiedName : qualifiedName.Substring(0, index);
    }

    /// <summary>
    /// Gets the class name of the given qualified constraint name.
    /// </summary>
    /// <param name="qualifiedName">The qualified name of the constraint.</param>
    /// <returns>The class name of the constraint.</returns>
    private static string GetClassNameOfConstraint(string qualifiedName)
    {
        var index = qualifiedName.LastIndexOf('.');
        return index == -1 ? qualifiedName : qualifiedName.Substring(0, index);
    }
}
