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
    public Dictionary<string, ProcessDataPart> Data { get; } = new Dictionary<string, ProcessDataPart>();

    /// <summary>
    /// Adds a data part to the process data.
    /// </summary>
    /// <param name="key">The key for the data part.</param>
    /// <param name="data">The data part to add.</param>
    public void AddData(string key, ProcessDataPart data)
    {
        this.Data[key] = data;
    }
}
