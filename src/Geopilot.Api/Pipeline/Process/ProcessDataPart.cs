namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents a part of the data processed by a pipeline process.
/// </summary>
public class ProcessDataPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessDataPart"/> class.
    /// </summary>
    /// <param name="data">The data for this part of the process.</param>
    public ProcessDataPart(object data)
    {
        this.Data = data;
    }

    /// <summary>
    /// The data for this part of the process.
    /// </summary>
    public object Data { get; }
}
