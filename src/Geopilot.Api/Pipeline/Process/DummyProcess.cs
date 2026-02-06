using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Dummy process for testing purposes.
/// </summary>
internal class DummyProcess : IPipelineProcess
{
    private const string InputMappingErrorLog = "error_log";
    private const string OutputMappingErrorLog = "error_log";

    private ILogger<DummyProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DummyProcess>();

    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required DataHandlingConfig DataHandlingConfig { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string>? Config { get; set; }

    /// <inheritdoc/>
    public ProcessData Run(ProcessData inputData)
    {
        // ToDo: Implement ILI validation logic here.
        var errorLogKey = DataHandlingConfig.GetInputMapping(InputMappingErrorLog);
        if (!inputData.Data.TryGetValue(errorLogKey, out var errorLogData))
        {
            var errorMessage = $"DummyProcess: input data does not contain required key '{InputMappingErrorLog}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        var outputData = new ProcessData();

        outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingErrorLog), new ProcessDataPart("DummyProcess: error log dummy data"));

        return outputData;
    }
}
