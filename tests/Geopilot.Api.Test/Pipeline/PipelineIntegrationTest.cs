using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.Api.Pipeline.Process.XtfValidation;
using Geopilot.Api.Validation.Interlis;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process.Container;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineIntegrationTest
{
    private static Guid jobId = Guid.Parse("b98559c5-b374-4cbc-a797-1b5a13a297e7");
    private static string interlisCheckServiceBaseUrl = "http://localhost/";
    private Mock<HttpMessageHandler> interlisValidatorMessageHandlerMock;
    private Mock<IOptions<PipelineOptions>> pipelineOptionsMock;
    private PipelineProcessFactory pipelineProcessFactory;
    private PipelineProcessFactory? containerAwareFactory;
    private Mock<ILogger> loggerMock;
    private Mock<ILoggerFactory> loggerFactoryMock;

    [TestInitialize]
    public void SetUp()
    {
        interlisValidatorMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        interlisValidatorMessageHandlerMock.Protected().Setup("Dispose", ItExpr.IsAny<bool>());

        var pipelineOptions = new PipelineOptions()
        {
            Definition = "myPipeline.yaml",
            Plugins = new List<string>(),
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                {
                    "Geopilot.Api.Pipeline.Process.XtfValidation.XtfValidatorProcess", new Parameterization()
                    {
                        { "checkServiceBaseUrl", interlisCheckServiceBaseUrl },
                    }
                },
            },
        };

        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        loggerMock = new Mock<ILogger>();
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        this.pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        containerAwareFactory?.Dispose();
        containerAwareFactory = null;
        pipelineProcessFactory?.Dispose();
    }

    [TestMethod]
    public async Task RunTwoStepPipelineRoadsExdm2ien()
    {
        var matcherStepId = "matcher";
        var validationStepId = "validation";
        var zipPackageStepId = "zip_package";
        var xtfFileAttribute = "xtfFiles";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new PipelineFileList(new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf"),
                new PipelineFile("TestData/UploadFiles/iseltwald_gwp_be13_1.xtf", "iseltwald_gwp_be13_1.xtf"),
            });
        using var pipeline = factory.CreatePipeline("two_steps_roadsexdm2ien", pipelineFiles, Guid.NewGuid());

        using HttpResponseMessage uploadMockResponse = new()
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(new InterlisUploadResponse()
            {
                JobId = jobId,
                StatusUrl = "/api/v1/status/" + jobId.ToString(),
            }),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/upload"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(uploadMockResponse);
        using HttpResponseMessage getStatusMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(new InterlisStatusResponse()
            {
                JobId = jobId,
                LogUrl = "/api/v1/download?jobId=" + jobId.ToString() + "&logType=log",
                XtfLogUrl = "/api/v1/download?jobId=" + jobId.ToString() + "&logType=xtf",
                Status = InterlisStatusResponseStatus.Completed,
                StatusMessage = "Validation successful",
            }),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/status/" + jobId.ToString()),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getStatusMockResponse);
        using FileStream appLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.log", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getAppLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(appLogFile),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=log"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getAppLogMockResponse);
        using FileStream xtfLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.xtf", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getXtfLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(xtfLogFile),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=xtf"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getXtfLogMockResponse);

        pipeline.Steps
            .Select(s => s.Process)
            .Where(p => p is XtfValidatorProcess)
            .Cast<XtfValidatorProcess>()
            .ToList()
            .ForEach(p =>
            {
                var httpClient = new HttpClient(interlisValidatorMessageHandlerMock.Object);
                httpClient.BaseAddress = new Uri(interlisCheckServiceBaseUrl);
                typeof(XtfValidatorProcess)
                    ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(p, httpClient);
            });

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var context = await pipeline.Run(CancellationToken.None);

        Assert.AreEqual(PipelineState.Success, pipeline.State);
        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[2].State);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;
        var matcherStepResult = stepResults[matcherStepId];
        var xtfFileStepOutput = matcherStepResult.Outputs[xtfFileAttribute];

        Assert.IsNotNull(xtfFileStepOutput.Data);
        var xtfFiles = xtfFileStepOutput.Data as IPipelineFile[];
        Assert.HasCount(1, xtfFiles);
        Assert.AreEqual("RoadsExdm2ien.xtf", xtfFiles[0].OriginalFileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);
        Assert.IsTrue(stepResults.ContainsKey(validationStepId));
        var validationSetpResult = stepResults[validationStepId];
        Assert.HasCount(3, validationSetpResult.Outputs, "validation step has not the expected number of data");

        Assert.IsTrue(stepResults.ContainsKey(zipPackageStepId));
        var zipPackageStepResult = stepResults[zipPackageStepId];
        Assert.HasCount(1, zipPackageStepResult.Outputs, "ZIP package step has not the expected number of data");
        zipPackageStepResult.Outputs.TryGetValue("archive", out StepOutput? zipFileStepOutput);
        Assert.IsNotNull(zipFileStepOutput, "No ZIP package in output");
        var zipFile = zipFileStepOutput.Data as IPipelineFile;
        Assert.IsNotNull(zipFile, "No ZIP file in output");
        Assert.AreEqual("myPersonalZipArchive.zip", zipFile.OriginalFileName, "ZIP file has not the expected name");

        interlisValidatorMessageHandlerMock.Verify();
        pipelineOptionsMock.Verify();
    }

    [TestMethod]
    public async Task RunTwoStepPipelineAmbiguousModel()
    {
        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new PipelineFileList(new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien1.xtf"),
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien2.xtf"),
            });
        using var pipeline = factory.CreatePipeline("two_steps_roadsexdm2ien", pipelineFiles, Guid.NewGuid());

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipeline.Run(CancellationToken.None));
        Assert.IsNotNull(exception);
        Assert.AreEqual("<2> values found for parameter <iliFile> of type <Geopilot.PipelineCore.Pipeline.IPipelineFile> in process run method.", exception.Message);
    }

    [TestMethod]
    public async Task RunTwoStepPipelineSkipsValidationWhenMultipleMatches()
    {
        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new PipelineFileList(new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien1.xtf"),
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien2.xtf"),
            });
        using var pipeline = factory.CreatePipeline("two_steps_skip_validation", pipelineFiles, Guid.NewGuid());

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(3, pipeline.Steps);

        var context = await pipeline.Run(CancellationToken.None);

        Assert.AreEqual(PipelineState.Success, pipeline.State);
        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Skipped, pipeline.Steps[1].State);
        Assert.AreEqual(StepState.Skipped, pipeline.Steps[2].State);

        // Assert matcher step produced 2 matched files
        var stepResults = context.StepResults;
        var matcherStepResult = stepResults["matcher"];
        var xtfFileStepOutput = matcherStepResult.Outputs["xtfFiles"];
        Assert.IsNotNull(xtfFileStepOutput.Data);
        var xtfFiles = xtfFileStepOutput.Data as IPipelineFile[];
        Assert.HasCount(2, xtfFiles);
    }

    [TestMethod]
    public async Task RunXtfToGpkgDeliveryPipeline()
    {
        // The ili2gpkg container is simulated by a mocked IContainerRunner — we cannot run the real
        // ili2gpkg image in unit tests (it's not publicly available on a registry we can rely on).
        // DockerContainerRunnerTest (tagged RequiresDocker) independently proves the Docker plumbing;
        // here we test the full pipeline wiring end-to-end with a faked container that produces a gpkg.
        var runnerMock = new Mock<IContainerRunner>(MockBehavior.Strict);
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<ContainerRunSpec>(), It.IsAny<CancellationToken>()))
            .Callback<ContainerRunSpec, CancellationToken>((spec, _) =>
            {
                // ili2gpkg would write the gpkg to /data/out/<basename>.gpkg; simulate that on the host bind-mount.
                var outMount = spec.VolumeMounts.First(m => m.ContainerPath == "/data/out");
                var dbfileIdx = spec.Command.ToList().IndexOf("--dbfile") + 1;
                var containerDbPath = spec.Command[dbfileIdx];
                var fileName = Path.GetFileName(containerDbPath);
                File.WriteAllBytes(Path.Combine(outMount.HostPath, fileName), System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0simulated"));
            })
            .ReturnsAsync(new ContainerRunResult(0, string.Empty, string.Empty, TimeSpan.FromMilliseconds(100)));

        PipelineFactory factory = CreatePipelineFactory("xtfToGpkgDelivery", runnerMock.Object);

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        var pipelineFiles = new PipelineFileList(new List<IPipelineFile>
            {
                new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf"),
            });
        using var pipeline = factory.CreatePipeline("xtf_to_gpkg_delivery", pipelineFiles, Guid.NewGuid());

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(4, pipeline.Steps);

        // Mock the InterlisCheck service HTTP calls so only the ili2gpkg container talks to real Docker.
        using HttpResponseMessage uploadMockResponse = new()
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(new InterlisUploadResponse
            {
                JobId = jobId,
                StatusUrl = "/api/v1/status/" + jobId.ToString(),
            }),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString() == interlisCheckServiceBaseUrl + "api/v1/upload"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(uploadMockResponse);

        using HttpResponseMessage getStatusMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(new InterlisStatusResponse
            {
                JobId = jobId,
                LogUrl = "/api/v1/download?jobId=" + jobId.ToString() + "&logType=log",
                XtfLogUrl = "/api/v1/download?jobId=" + jobId.ToString() + "&logType=xtf",
                Status = InterlisStatusResponseStatus.Completed,
                StatusMessage = "Validation successful",
            }),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == interlisCheckServiceBaseUrl + "api/v1/status/" + jobId.ToString()),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getStatusMockResponse);

        using FileStream appLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.log", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getAppLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(appLogFile),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=log"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getAppLogMockResponse);

        using FileStream xtfLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.xtf", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getXtfLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(xtfLogFile),
        };
        interlisValidatorMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=xtf"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(getXtfLogMockResponse);

        pipeline.Steps
            .Select(s => s.Process)
            .OfType<XtfValidatorProcess>()
            .ToList()
            .ForEach(p =>
            {
                var httpClient = new HttpClient(interlisValidatorMessageHandlerMock.Object)
                {
                    BaseAddress = new Uri(interlisCheckServiceBaseUrl),
                };
                typeof(XtfValidatorProcess)
                    .GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(p, httpClient);
            });

        var context = await pipeline.Run(CancellationToken.None);

        Assert.AreEqual(PipelineState.Success, pipeline.State, "pipeline did not succeed");
        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "delivery not allowed");
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State, "matcher step did not succeed");
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State, "validation step did not succeed");
        Assert.AreEqual(StepState.Success, pipeline.Steps[2].State, "convert step did not succeed");
        Assert.AreEqual(StepState.Success, pipeline.Steps[3].State, "zip step did not succeed");

        // Assert the convert step produced at least one .gpkg file.
        var convertOutputs = context.StepResults["convert"].Outputs;
        Assert.IsTrue(convertOutputs.ContainsKey("gpkgFiles"), "convert step did not expose gpkgFiles output");
        var gpkgFiles = convertOutputs["gpkgFiles"].Data as IPipelineFile[];
        Assert.IsNotNull(gpkgFiles, "gpkgFiles output is not an IPipelineFile[]");
        Assert.IsNotEmpty(gpkgFiles, "no files produced");
        Assert.IsTrue(gpkgFiles.Any(f => string.Equals(f.FileExtension, "gpkg", StringComparison.OrdinalIgnoreCase)), "no file with .gpkg extension produced");

        // Assert the gpkg file is a plausible SQLite file (magic header bytes "SQLite format 3\0").
        var gpkgFile = gpkgFiles.First(f => string.Equals(f.FileExtension, "gpkg", StringComparison.OrdinalIgnoreCase));
        using (var stream = gpkgFile.OpenReadFileStream())
        {
            var header = new byte[16];
            var read = stream.Read(header, 0, header.Length);
            Assert.AreEqual(16, read, "gpkg file is unexpectedly short");
            var headerText = System.Text.Encoding.ASCII.GetString(header);
            StringAssert.StartsWith(headerText, "SQLite format 3", "gpkg file does not have the SQLite magic header");
        }
    }

    private PipelineFactory CreatePipelineFactory(string filename, IContainerRunner? containerRunner = null)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        var fileAccessOptions = new FileAccessOptions()
        {
            UploadDirectory = Path.Combine(Path.GetTempPath(), "Upload"),
            AssetsDirectory = Path.Combine(Path.GetTempPath(), "Asset"),
            PipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline"),
        };

        // If a container runner is supplied, build a fresh factory that has it wired in; otherwise reuse the SetUp-constructed one.
        // The fresh factory is kept in the containerAwareFactory field so TestCleanup can dispose it.
        IPipelineProcessFactory processFactory;
        if (containerRunner != null)
        {
            var freshFactory = new PipelineProcessFactory(this.pipelineOptionsMock.Object, this.loggerFactoryMock.Object, containerRunner);
            this.containerAwareFactory = freshFactory;
            processFactory = freshFactory;
        }
        else
        {
            processFactory = this.pipelineProcessFactory;
        }

        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(processFactory)
            .LoggerFactory(this.loggerFactoryMock.Object)
            .DirectoryProvider(new DirectoryProvider(Options.Create(fileAccessOptions)))
            .Build();
    }
}
