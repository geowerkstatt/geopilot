using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Processes.XtfValidatorErrorTree;

internal class XtfValidatorErrorTreeProcess
{
    private const string OutputMappingVisualization = "visualization";
    private const string OutputMappingStatusMessage = "status_message";

    private static readonly Dictionary<string, string> SuccessfulStatusMessage = new Dictionary<string, string>
        {
            { "de", "Error Tree erstellt" },
            { "fr", "Arbre d'erreurs créé" },
            { "it", "Albero degli errori creato" },
            { "en", "Error tree created" },
        };

    [PipelineProcessRun]
    public Task<Dictionary<string, object?>> RunAsync(IPipelineFile xtfLog)
    {
        var errors = XtfLogParser.Parse(xtfLog);
        var errorTreeMapper = new LogErrorToErrorTreeMapper(errors);
        var config = new TreeVisualizationConfig { Nodes = errorTreeMapper.Map() };

        return Task.FromResult(new Dictionary<string, object?>
        {
            { OutputMappingVisualization, VisualizationFactory.Tree(config) },
            { OutputMappingStatusMessage, SuccessfulStatusMessage },
        });
    }
}
