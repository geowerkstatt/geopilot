using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class VsaMatcherIntegrationTest
{
    private const string VsaMatcherImplementation = "VsaCheckerAnalytics.Process.Matcher.VsaMatcher.VsaMatcherProcess";
    private const string OrgTableUrl2020 = "http://test.local/org_2020.xtf";
    private const string OrgTableUrl20201 = "http://test.local/org_2020_1.xtf";
    private static readonly string TestProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    private static readonly string PluginDir = Path.Combine(TestProjectDir, "Plugins", "VsaCheckerAnalytics");
    private static readonly string PluginDllPath = Path.Combine(PluginDir, "VSACheckerAnalytics.dll");
    private static readonly string ResourceDir = Path.Combine(PluginDir, "Resources");

    private static readonly List<OutputConfig> VsaMatcherOutputs =
    [
        new() { Take = "gep", As = "gep" },
        new() { Take = "model_version", As = "model_version" },
        new() { Take = "language", As = "language" },
        new() { Take = "user_org_table", As = "user_org_table" },
        new() { Take = "checker_csv_a", As = "checker_csv_a" },
        new() { Take = "checker_csv_fp", As = "checker_csv_fp" },
        new() { Take = "checker_csv_t", As = "checker_csv_t" },
        new() { Take = "gpkg_template", As = "gpkg_template" },
        new() { Take = "standard_org_table", As = "standard_org_table" },
        new() { Take = "error_matrix", As = "error_matrix" },
    ];

    private Mock<HttpMessageHandler> httpMessageHandlerMock = null!;
    private HttpClient httpClient = null!;
    private PipelineProcessFactory pipelineProcessFactory = null!;
    private Mock<ILoggerFactory> loggerFactoryMock = null!;
    private string tempDir = null!;

    [TestInitialize]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "vsa-matcher-integration-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpMessageHandlerMock.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
        SetupOrgTableMock(OrgTableUrl2020);
        SetupOrgTableMock(OrgTableUrl20201);

        var pipelineOptions = new PipelineOptions
        {
            Definition = "unused",
            Plugins = [PluginDllPath],
            ProcessConfigs = new Dictionary<string, Parameterization>
            {
                {
                    VsaMatcherImplementation, new Parameterization
                    {
                        { "geoPackageTemplatePath2020", Path.Combine(ResourceDir, "template_ca_dssmini_2020_d.gpkg") },
                        { "geoPackageTemplatePath20201", Path.Combine(ResourceDir, "template_ca_dssmini_2020_1_d.gpkg") },
                        { "errorMatrixPath", Path.Combine(ResourceDir, "ErrorMatrix_v3.xlsx") },
                        { "vsaOrgTableUrl2020", OrgTableUrl2020 },
                        { "vsaOrgTableUrl20201", OrgTableUrl20201 },
                    }
                },
            },
        };

        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);

        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        httpClient?.Dispose();
        pipelineProcessFactory?.Dispose();
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch (IOException) { }
        }
    }

    [TestMethod]
    public async Task RunVsaMatcherPipeline()
    {
        if (!File.Exists(PluginDllPath))
            Assert.Inconclusive("VSA plugin not available. Run scripts/Copy-VsaPlugin.ps1 first.");

        var gepXtfPath = CreateGepXtf("VSADSSMINI_2020_LV95");

        var stepConfig = new StepConfig
        {
            Id = "vsa_matcher",
            DisplayName = new Dictionary<string, string> { { "en", "VSA Matching" } },
            ProcessId = "vsa_matcher",
            Output = VsaMatcherOutputs,
        };

        var processes = new List<ProcessConfig>
        {
            new() { Id = "vsa_matcher", Implementation = VsaMatcherImplementation },
        };

        var process = pipelineProcessFactory.Builder()
            .PipelineId("test")
            .StepConfig(stepConfig)
            .Processes(processes)
            .PipelineDirectory(tempDir)
            .JobId(Guid.NewGuid())
            .Build();

        var inputConfig = new List<InputConfig>
        {
            new() { From = "unzipper", Take = "extracted_files", As = "unzippedFiles" },
        };

        using var step = PipelineStep.Builder()
            .Id("vsa_matcher")
            .DisplayName(new Dictionary<string, string> { { "en", "VSA Matching" } })
            .InputConfig(inputConfig)
            .OutputConfig(VsaMatcherOutputs)
            .Process(process)
            .Logger(new Mock<ILogger>().Object)
            .Build();

        ReplaceHttpClient(step.Process);

        var unzipResult = new StepResult();
        unzipResult.Outputs["extracted_files"] = new StepOutput
        {
            Data = new IPipelineFile[]
            {
                CreateCsvFile("gep_a_err.csv", "check"),
                CreateCsvFile("gep_fp_err.csv", "check"),
                CreateCsvFile("gep_t_err.csv", "check"),
            },
            Action = [],
        };

        var context = new PipelineContext
        {
            Upload = new PipelineFileList([new PipelineFile(gepXtfPath, "gep_vsadssmini_2020.xtf")]),
            StepResults = new Dictionary<string, StepResult> { { "unzipper", unzipResult } },
        };

        var result = await step.Run(context, CancellationToken.None);

        Assert.AreEqual(StepState.Success, step.State);

        var gepFiles = result.Outputs["gep"].Data as IPipelineFile[];
        Assert.IsNotNull(gepFiles);
        Assert.HasCount(1, gepFiles);

        Assert.AreEqual("2020", result.Outputs["model_version"].Data);
        Assert.AreEqual("DE", result.Outputs["language"].Data);

        var checkerCsvA = result.Outputs["checker_csv_a"].Data as IPipelineFile[];
        var checkerCsvFp = result.Outputs["checker_csv_fp"].Data as IPipelineFile[];
        var checkerCsvT = result.Outputs["checker_csv_t"].Data as IPipelineFile[];
        Assert.IsNotNull(checkerCsvA);
        Assert.IsNotNull(checkerCsvFp);
        Assert.IsNotNull(checkerCsvT);
        Assert.HasCount(1, checkerCsvA);
        Assert.HasCount(1, checkerCsvFp);
        Assert.HasCount(1, checkerCsvT);

        Assert.IsNotNull(result.Outputs["gpkg_template"].Data);
        Assert.IsNotNull(result.Outputs["standard_org_table"].Data);
        Assert.IsNotNull(result.Outputs["error_matrix"].Data);
    }

    private void ReplaceHttpClient(object process)
    {
        httpClient = new HttpClient(httpMessageHandlerMock.Object);
        process.GetType()
            .GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(process, httpClient);
    }

    private void SetupOrgTableMock(string url)
    {
#pragma warning disable CA2000 // Response is disposed by the code under test
        _ = httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("<org-table/>"),
            });
#pragma warning restore CA2000
    }

    private PipelineFile CreateCsvFile(string fileName, string relativePath)
    {
        var path = Path.Combine(tempDir, relativePath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "col1;col2\nval1;val2");
        return new PipelineFile(path, fileName, relativePath);
    }

    private string CreateGepXtf(string modelName)
    {
        var xtf = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<ili:transfer xmlns:ili=\"http://www.interlis.ch/xtf/2.4/INTERLIS\">\n"
            + "  <ili:headersection>\n"
            + "    <ili:models>\n"
            + $"      <ili:model>{modelName}</ili:model>\n"
            + "    </ili:models>\n"
            + "    <ili:sender>IntegrationTest</ili:sender>\n"
            + "  </ili:headersection>\n"
            + "  <ili:datasection/>\n"
            + "</ili:transfer>";

        var path = Path.Combine(tempDir, "gep.xtf");
        File.WriteAllText(path, xtf);
        return path;
    }
}
