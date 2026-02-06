using Geopilot.Api.Pipeline.Config;
using System.Net.Http.Headers;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Process for validating ILI files.
/// </summary>
internal class IliValidatorProcess : IPipelineProcess, IDisposable
{
    private const string InputMappingIliFile = "ili_file";
    private const string OutputMappingErrorLog = "error_log";
    private const string OutputMappingXtfLog = "xtf_log";
    private const string InterlisCheckServiceBaseAddressConfiguration = "Validation:InterlisCheckServiceUrl";

    private ILogger<IliValidatorProcess> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<IliValidatorProcess>();

    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required DataHandlingConfig DataHandlingConfig { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string>? Config { get; set; }

    private HttpClient? httpClient;

    public void Dispose()
    {
        httpClient?.Dispose();
    }

    [PipelineProcessInitialize]
    public void Initialize(IConfiguration configuration)
    {
        var checkServiceUrl = configuration.GetValue<string>(InterlisCheckServiceBaseAddressConfiguration)
            ?? throw new InvalidOperationException("Missing InterlisCheckServiceUrl to validate INTERLIS transfer files.");
        httpClient = new() { BaseAddress = new Uri(checkServiceUrl), };

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private ProcessDataPart InputIliFile(ProcessData inputData)
    {
        var inputIliFileKey = DataHandlingConfig.GetInputMapping(InputMappingIliFile);
        if (inputIliFileKey == null || !inputData.Data.TryGetValue(inputIliFileKey, out var iliFilePart))
        {
            var errorMessage = $"IliValidatorProcess: input data does not contain required key '{InputMappingIliFile}'.";
            logger.LogError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return iliFilePart;
    }

    /// <inheritdoc/>
    public ProcessData Run(ProcessData inputData)
    {
        // ToDo: Implement ILI validation logic here.
        object inputIliFile = InputIliFile(inputData);

        var outputData = new ProcessData();

        outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingErrorLog), new ProcessDataPart("IliValidatorProcess: error log dummy data"));
        outputData.AddData(DataHandlingConfig.GetOutputMapping(OutputMappingXtfLog), new ProcessDataPart("IliValidatorProcess: xtf log dummy data"));

        return outputData;
    }
}
