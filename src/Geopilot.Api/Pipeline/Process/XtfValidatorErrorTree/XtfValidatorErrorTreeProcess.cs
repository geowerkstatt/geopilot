using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

internal class XtfValidatorErrorTreeProcess
{
    private const string OutputMappingErrorLog = "error_tree";
    private const string OutputMappingJsonErrorLog = "json_error_tree";
    private const string OutputMappingTreeConfig = "tree_config";
    private const string OutputMappingStatusMessage = "status_message";

    private static readonly Dictionary<string, string> SuccessfulStatusMessage = new Dictionary<string, string>
        {
            { "de", "Error Tree erstellt" },
            { "fr", "Arbre d'erreurs créé" },
            { "it", "Albero degli errori creato" },
            { "en", "Error tree created" },
        };

    private static readonly JsonSerializerOptions JsonOptions;
    private readonly IPipelineFileManager pipelineFileManager;

    static XtfValidatorErrorTreeProcess()
    {
        JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public XtfValidatorErrorTreeProcess(IPipelineFileManager pipelineFileManager)
    {
        this.pipelineFileManager = pipelineFileManager;
    }

    [PipelineProcessRun]
    public async Task<Dictionary<string, object?>> RunAsync(IPipelineFile xtfLog)
    {
        using var xtfLogFileStream = xtfLog.OpenReadFileStream();

        var xtfErrors = XtfLogParser.Parse(new StreamReader(xtfLogFileStream));

        var errorTreeMapper = new LogErrorToErrorTreeMapper(xtfErrors);
        var errorLog = errorTreeMapper.Map();
        var jsonErrorLog = JsonSerializer.Serialize(errorLog, JsonOptions);

        var treeConfigFile = pipelineFileManager.GeneratePipelineFile("treeConfig", "json");

        using (FileStream fileStream = treeConfigFile.OpenWriteFileStream())
        using (StreamWriter streamWriter = new(fileStream))
        {
            await streamWriter.WriteAsync(jsonErrorLog);
        }

        return new Dictionary<string, object?>()
        {
            { OutputMappingErrorLog, errorLog },
            { OutputMappingJsonErrorLog, jsonErrorLog },
            { OutputMappingTreeConfig, treeConfigFile },
            { OutputMappingStatusMessage, SuccessfulStatusMessage },
        };
    }
}
