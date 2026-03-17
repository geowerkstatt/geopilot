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
    private Guid jobId;

    static XtfValidatorErrorTreeProcess()
    {
        JsonOptions = new() { };
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public XtfValidatorErrorTreeProcess(Guid jobId)
    {
        this.jobId = jobId;
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineTransferFile xtfLog)
    {
        var xtfLogFileStream = xtfLog.OpenFileStream();

        var xtfErrors = XtfLogParser.Parse(new StreamReader(xtfLogFileStream));

        var errorTreeMapper = new LogErrorToErrorTreeMapper(xtfErrors);
        var errorLog = errorTreeMapper.Map();
        var jsonErrorLog = JsonSerializer.Serialize(errorLog, JsonOptions);
        var jsonErrorLogFile = new PipelineTransferFile("errorTree", Path.GetTempFileName().Replace(".tmp", ".json"));

        using FileStream fileStream = File.OpenWrite(jsonErrorLogFile.FilePath);
        await using StreamWriter streamWriter = new(fileStream);
        await streamWriter.WriteAsync(jsonErrorLog);

        return new Dictionary<string, object?>()
        {
            { OutputMappingErrorLog, errorLog },
            { OutputMappingJsonErrorLog, jsonErrorLog },
            { OutputMappingJsonErrorLogFile, jsonErrorLogFile },
        };
    }
}
