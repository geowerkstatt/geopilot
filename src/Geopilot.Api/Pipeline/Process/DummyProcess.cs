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

    private DataHandlingConfig? dataHandlingConfig;

    private Dictionary<string, string>? config;

    /// <summary>
    /// Initializes the pipeline process with the specified configuration settings.
    /// </summary>
    /// <param name="config">A dictionary containing configuration key-value pairs to be used for initialization. Cannot be null.</param>
    [PipelineProcessInitialize]
    public void Initialize(Dictionary<string, string> config)
    {
        this.config = config;
    }

    /// <summary>
    /// Initializes the pipeline process with the specified data handling configuration.
    /// </summary>
    /// <param name="dataHandlingConfig">The data handling configuration to be used for the pipeline process. Cannot be null.</param>
    [PipelineProcessInitialize]
    public void Initialize(DataHandlingConfig dataHandlingConfig)
    {
        this.dataHandlingConfig = dataHandlingConfig;
    }

    /// <inheritdoc/>
    public async Task<ProcessData> Run(ProcessData inputData)
    {
        // ToDo: Implement ILI validation logic here.
        var errorLogKey = dataHandlingConfig?.GetInputMapping(InputMappingErrorLog);
        if (errorLogKey == null || !inputData.Data.TryGetValue(errorLogKey, out var errorLogData))
        {
            var errorMessage = $"DummyProcess: input data does not contain required key '{InputMappingErrorLog}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        var outputData = new ProcessData();

        if (dataHandlingConfig != null)
            outputData.AddData(dataHandlingConfig.GetOutputMapping(OutputMappingErrorLog), new ProcessDataPart("DummyProcess: error log dummy data"));

        return outputData;
    }
}
