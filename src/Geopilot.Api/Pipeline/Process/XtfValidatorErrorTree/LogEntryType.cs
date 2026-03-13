namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Specifies the severity level of a log entry.
/// </summary>
/// <remarks>Use this enumeration to indicate whether a log entry represents informational messages, warnings, or
/// errors. The value helps consumers of log data filter or process entries based on their importance.</remarks>
public enum LogEntryType
{
    /// <summary>
    /// Informational message or data associated with the current context.
    /// </summary>
    Info,

    /// <summary>
    /// Represents a warning message or status within the application.
    /// </summary>
    Warning,

    /// <summary>
    /// Represents an error condition or status within the application.
    /// </summary>
    Error,
}
