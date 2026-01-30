namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents a part of the data processed by a pipeline process.
/// </summary>
public class ProcessDataPart
{
    /// <summary>
    /// The data for this part of the process.
    /// </summary>
    public required object Data { get; set; }
}
