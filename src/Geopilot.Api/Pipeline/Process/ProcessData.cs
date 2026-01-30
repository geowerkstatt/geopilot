namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Represents the input and output data processed by a pipeline process.
/// </summary>
public class ProcessData
{
    /// <summary>
    /// The dictionary containing the data parts for the process.
    /// The key is a string identifier, and the value is a ProcessDataPart object.
    /// It is configured via <see cref="IPipelineProcess.DataHandlingConfig"/>.
    /// </summary>
    public Dictionary<string, ProcessDataPart> Data { get; set; } = new Dictionary<string, ProcessDataPart>();
}
