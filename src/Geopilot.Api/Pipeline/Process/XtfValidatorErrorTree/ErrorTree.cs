using System.Diagnostics;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Represents a hierarchical log entry.
/// </summary>
[DebuggerDisplay("Message = {Message}, Type = {Type}, Children = {Values.Count}")]
public class ErrorTree
{
    /// <summary>
    /// Gets or sets the message text associated with this instance.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the type of the log entry.
    /// </summary>
    public required LogEntryType Type { get; set; }

    /// <summary>
    /// Gets or sets the collection of child log entries associated with this instance.
    /// </summary>
    public required IList<ErrorTree> Values { get; set; }

    /// <summary>
    /// Determines whether the specified object is equal to the current HierarchicalLogEntry instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current HierarchicalLogEntry. Can be null.</param>
    /// <returns>true if the specified object is a HierarchicalLogEntry and has the same Message, Type, and Values as the current
    /// instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not ErrorTree other)
            return false;

        var messageEquals = Message == other.Message;
        var typeEquals = Type == other.Type;
        var valuesEquals = Values.SequenceEqual(other.Values);

        return messageEquals && typeEquals && valuesEquals;
    }

    /// <summary>
    /// Serves as the default hash function for the object.
    /// </summary>
    /// <remarks>The hash code is computed based on the values of the Message, Type, and Values properties.
    /// Objects with identical property values will produce the same hash code. This method is suitable for use in
    /// hashing algorithms and data structures such as hash tables.</remarks>
    /// <returns>A 32-bit signed integer hash code that represents the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Message, Type, Values);
    }
}
