using Geopilot.Api.FileAccess;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Geopilot.Api;

[TestClass]
public sealed class WebApplicationExtensionsTest
{
    // A structurally valid pipeline whose only fault is a cross-step type mismatch: the second step wires
    // the first step's StatusMessage (a LocalizedText) into the IPipelineFile run parameter 'xtfLog'.
    // Catching this requires the per-step validation to see the whole step list, which
    // ValidatePipelineConfiguration supplies via .StepResultTypes(stepResultTypes). Remove that one line and the
    // reference is left unchecked, the app boots, and this test fails.
    private const string CrossStepTypeMismatchPipeline = """
        processes:
          - id: xtf_matcher
            implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
            default_config:
              fileExtensions:
                - xtf
          - id: xtf_error_visualizer
            implementation: Geopilot.Pipeline.Processes.XtfErrorVisualization.XtfErrorVisualizationProcess
        pipelines:
          - id: bad_pipeline
            display_name:
              en: Bad pipeline
              de: Bad pipeline
            steps:
              - id: xtf_matching
                display_name:
                  en: XTF Matching
                  de: XTF Zuordnung
                process_id: xtf_matcher
                input:
                  files: "${upload()}"
              - id: error_visualization
                display_name:
                  en: Error Visualization
                  de: Fehlervisualisierung
                process_id: xtf_error_visualizer
                input:
                  xtfLog: "${step_output(xtf_matching.StatusMessage)}"
                output_actions:
                  - property: StatusMessage
                    actions:
                      - StatusMessage
        """;

    [TestMethod]
    public void ValidatePipelineConfigurationRejectsCrossStepTypeMismatch()
    {
        var yamlPath = Path.Combine(Path.GetTempPath(), $"badPipeline_{Guid.NewGuid():N}.yaml");
        File.WriteAllText(yamlPath, CrossStepTypeMismatchPipeline);
        try
        {
            using var app = BuildValidationApp(yamlPath);

            var exception = Assert.Throws<InvalidOperationException>(() => app.ValidatePipelineConfiguration());

            StringAssert.Contains(exception.Message, "xtf_matching.StatusMessage");
            StringAssert.Contains(exception.Message, "is not compatible with the parameter type <IPipelineFile>");
        }
        finally
        {
            File.Delete(yamlPath);
        }
    }

    private static WebApplication BuildValidationApp(string pipelineDefinitionPath)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pipeline:Definition"] = pipelineDefinitionPath,
            ["Ilitools:IlitoolsWrapperAddress"] = "http://localhost:5000",
        });

        builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
        builder.Services.Configure<IlitoolsOptions>(builder.Configuration.GetSection(IlitoolsOptions.SectionName));
        builder.Services.AddSingleton<IPipelineProcessFactory, PipelineProcessFactory>();
        builder.Services.AddPipelineFactory();
        builder.Services.AddSingleton<IDirectoryProvider>(AssemblyInitialize.TestDirectoryProvider);

        return builder.Build();
    }
}
