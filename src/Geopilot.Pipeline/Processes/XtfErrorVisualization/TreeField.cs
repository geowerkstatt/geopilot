namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// The categorical fields of a <see cref="TreeItem"/> the frontend can group and filter the error tree by.
/// Configured per pipeline step (<c>groupBy</c>/<c>filterBy</c>, parsed case-insensitively from the pipeline
/// definition) and serialized to JSON as camelCase names, matching the frontend's field union.
/// </summary>
internal enum TreeField
{
    /// <summary>
    /// The classified error category (<see cref="TreeItem.ErrorType"/>).
    /// </summary>
    ErrorType,

    /// <summary>
    /// The INTERLIS model of the failing object (<see cref="TreeItem.Model"/>).
    /// </summary>
    Model,

    /// <summary>
    /// The INTERLIS topic of the failing object (<see cref="TreeItem.Topic"/>).
    /// </summary>
    Topic,

    /// <summary>
    /// The INTERLIS class of the failing object (<see cref="TreeItem.Class"/>).
    /// </summary>
    Class,
}
