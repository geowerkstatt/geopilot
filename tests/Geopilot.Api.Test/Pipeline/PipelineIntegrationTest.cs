using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process;
using Geopilot.Api.Validation.Interlis;
using Microsoft.Extensions.Configuration;
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
    private IConfiguration configuration;

    [TestInitialize]
    public void SetUp()
    {
        interlisValidatorMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        interlisValidatorMessageHandlerMock.Protected().Setup("Dispose", ItExpr.IsAny<bool>());

        var inMemorySettings = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Validation:InterlisCheckServiceUrl", interlisCheckServiceBaseUrl),
        };

        #pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        this.configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        #pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
    }

    [TestCleanup]
    public void Cleanup()
    {
        interlisValidatorMessageHandlerMock.VerifyAll();
    }

    [TestMethod]
    public async Task RunTwoStepPipeline()
    {
        var uploadStepId = "upload";
        var validationStepId = "validation";
        var zipPackageStepId = "zip_package";
        var uploadedFileAttribute = "ili_file";

        PipelineFactory factory = CreatePipelineFactory("twoStepPipeline_01");
        using var pipeline = factory.CreatePipeline("two_steps");

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
            .Where(p => p is IliValidatorProcess)
            .Cast<IliValidatorProcess>()
            .ToList()
            .ForEach(p =>
            {
                var httpClient = new HttpClient(interlisValidatorMessageHandlerMock.Object);
                httpClient.BaseAddress = new Uri(interlisCheckServiceBaseUrl);
                typeof(IliValidatorProcess)
                    ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(p, httpClient);
            });

        Assert.IsNotNull(pipeline, "pipeline not created");
        Assert.HasCount(2, pipeline.Steps);

        PipelineTransferFile uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var context = await pipeline.Run(uploadFile, CancellationToken.None);

        Assert.AreEqual(PipelineState.Success, pipeline.State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[0].State);
        Assert.AreEqual(StepState.Success, pipeline.Steps[1].State);

        // Assert if uploaded file was correctly added to PipelineContext
        var stepResults = context.StepResults;

        Assert.IsTrue(stepResults.ContainsKey(uploadStepId));
        var uploadStepResult = context.StepResults[uploadStepId];
        Assert.HasCount(1, uploadStepResult.Outputs, "upload step has not the expected number of data");
        Assert.IsTrue(uploadStepResult.Outputs.ContainsKey(uploadedFileAttribute));
        var uploadedFileStepOutput = uploadStepResult.Outputs[uploadedFileAttribute];

        Assert.IsNotNull(uploadedFileStepOutput.Data);
        var uploadedFile = uploadedFileStepOutput.Data as IPipelineTransferFile;
        Assert.IsNotNull(uploadedFile);
        Assert.AreEqual(uploadFile.FilePath, uploadedFile.FilePath);

        // Assert if StepResults from executed PipelineSteps are in the PipelineContext
        Assert.HasCount(3, stepResults);
        Assert.IsTrue(stepResults.ContainsKey(validationStepId));
        var validationSetpResult = stepResults[validationStepId];
        Assert.HasCount(2, validationSetpResult.Outputs, "validation step has not the expected number of data");

        Assert.IsTrue(stepResults.ContainsKey(zipPackageStepId));
        var zipPackageStepResult = stepResults[zipPackageStepId];
        Assert.HasCount(1, zipPackageStepResult.Outputs, "dummy step has not the expected number of data");
    }

    private PipelineFactory CreatePipelineFactory(string filename)
    {
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"TestData/Pipeline/" + filename + ".yaml");
        return PipelineFactory
            .Builder()
            .File(path)
            .Configuration(configuration)
            .Build();
    }
}
