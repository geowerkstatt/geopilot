using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

internal class XtfValidatorErrorTreeProcess
{
    private const string OutputMappingErrorLog = "error_tree";
    private const string OutputMappingJsonErrorLog = "json_error_tree";
    private const string OutputMappingJsonErrorLogFile = "json_error_tree_file";

    private static readonly JsonSerializerOptions JsonOptions;
    private readonly Guid jobId;
    private readonly IPipelineFileManager pipelineFileManager;

    static XtfValidatorErrorTreeProcess()
    {
        JsonOptions = new() { };
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public XtfValidatorErrorTreeProcess(IPipelineFileManager pipelineFileManager, Guid jobId)
    {
        this.pipelineFileManager = pipelineFileManager;
        this.jobId = jobId;
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineTransferFile xtfLog)
    {
        using var xtfLogFileStream = xtfLog.OpenReadFileStream();

        var xtfErrors = XtfLogParser.Parse(new StreamReader(xtfLogFileStream));

        var errorTreeMapper = new LogErrorToErrorTreeMapper(xtfErrors);
        var errorLog = errorTreeMapper.Map();
        var jsonErrorLog = JsonSerializer.Serialize(errorLog, JsonOptions);

        var jsonErrorLogFile = pipelineFileManager.GenerateTransferFile("errorTree", "json");

        using (FileStream fileStream = jsonErrorLogFile.OpenWriteFileStream())
        using (StreamWriter streamWriter = new(fileStream))
        {
            await streamWriter.WriteAsync(jsonErrorLog);
        }

        return new Dictionary<string, object?>()
        {
            { OutputMappingErrorLog, errorLog },
            { OutputMappingJsonErrorLog, jsonErrorLog },
            { OutputMappingJsonErrorLogFile, jsonErrorLogFile },
        };
    }
}
