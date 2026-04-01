using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.Api.Pipeline.Process.XtfValidation;
using Geopilot.Api.Validation.Interlis;
using Geopilot.PipelineCore.Pipeline;
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
        interlisValidatorMessageHandlerMock.VerifyAll();
        pipelineOptionsMock.VerifyAll();
    }

    [TestMethod]
    public async Task RunTwoStepPipeline()
    {
        var uploadStepId = "upload";
        var validationStepId = "validation";
        var zipPackageStepId = "zip_package";
        var uploadedFileAttribute = "ili_file";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");

        var validationErrors = factory.PipelineProcessConfig.Validate();
        Assert.HasCount(0, validationErrors, $"validation errors on Pipeline {validationErrors.ErrorMessage}");

        PipelineFile uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        using var pipeline = factory.CreatePipeline("two_steps", new PipelineFileList(new List<IPipelineFile> { uploadFile }), Guid.NewGuid());

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

        Assert.IsTrue(stepResults.ContainsKey(uploadStepId));
        var uploadStepResult = context.StepResults[uploadStepId];
        Assert.HasCount(1, uploadStepResult.Outputs, "upload step has not the expected number of data");
        Assert.IsTrue(uploadStepResult.Outputs.ContainsKey(uploadedFileAttribute));
        var uploadedFileStepOutput = uploadStepResult.Outputs[uploadedFileAttribute];

        Assert.IsNotNull(uploadedFileStepOutput.Data);
        var uploadedFile = uploadedFileStepOutput.Data as IPipelineFile;
        Assert.IsNotNull(uploadedFile);
        Assert.AreEqual(uploadFile.OriginalFileName, uploadedFile.OriginalFileName);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(4, stepResults);
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
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        var fileAccessOptions = new FileAccessOptions()
        {
            UploadDirectory = Path.Combine(Path.GetTempPath(), "Upload"),
            AssetsDirectory = Path.Combine(Path.GetTempPath(), "Asset"),
            PipelineDirectory = Path.Combine(Path.GetTempPath(), "Pipeline"),
        };
        return PipelineFactory
            .Builder()
            .File(path)
            .PipelineProcessFactory(this.pipelineProcessFactory)
            .LoggerFactory(this.loggerFactoryMock.Object)
            .DirectoryProvider(new DirectoryProvider(Options.Create(fileAccessOptions)))
            .Build();
    }
}
