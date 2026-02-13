using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process;
using Geopilot.Api.Validation;
using Geopilot.Api.Validation.Interlis;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class IliValidatorProcessTest
{
    private static Guid jobId = Guid.Parse("b98559c5-b374-4cbc-a797-1b5a13a297e7");

    [TestMethod]
    public void SunnyDay()
    {
        using HttpResponseMessage uploadMockResponse = new()
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(new InterlisUploadResponse()
            {
                JobId = jobId,
                StatusUrl = "/api/v1/status/" + jobId.ToString(),
            }),
        };
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
        using FileStream appLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.log", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getAppLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(appLogFile),
        };
        using FileStream xtfLogFile = File.Open(@"TestData/DownloadFiles/ilicop/log.xtf", FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using HttpResponseMessage getXtfLogMockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(xtfLogFile),
        };
        using var process = IliValidatorProcessBuilder.Create()
            .InputFile("file")
            .OutputErrorLog("error_log")
            .OutputXtfLog("xtf_log")
            .InterlisCheckServiceBaseUrl("http://localhost/")
            .UploadMockResponse(uploadMockResponse)
            .GetStatusMockResponse(getStatusMockResponse)
            .GetAppLogMockResponse(getAppLogMockResponse)
            .GetXtfLogMockResponse(getXtfLogMockResponse)
            .Build();
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processData = new ProcessData();
        processData.AddData("file", new ProcessDataPart(uploadFile));

        var processResult = Task.Run(() => process.Run(processData)).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult.Data);
        processResult.Data.TryGetValue("error_log", out var appLogData);
        Assert.IsNotNull(appLogData);
        var appLog = appLogData.Data as IPipelineTransferFile;
        Assert.IsNotNull(appLog);
        Assert.AreEqual("errorLog.log", appLog.OrginalFileName);
        processResult.Data.TryGetValue("xtf_log", out var xtfLogData);
        Assert.IsNotNull(xtfLogData);
        var xtfLog = xtfLogData.Data as IPipelineTransferFile;
        Assert.IsNotNull(xtfLog);
        Assert.AreEqual("xtfLog.xtf", xtfLog.OrginalFileName);
    }

    [TestMethod]
    public void InputIliFileNotCorrectType()
    {
        using var process = IliValidatorProcessBuilder.Create()
            .InputFile("file")
            .OutputErrorLog("error_log")
            .OutputXtfLog("xtf_log")
            .InterlisCheckServiceBaseUrl("http://localhost/")
            .Build();
        var processData = new ProcessData();
        processData.AddData("file", new ProcessDataPart("invalid data"));
        var exception = Assert.Throws<ArgumentException>(() => Task.Run(() => process.Run(processData)).GetAwaiter().GetResult());
        Assert.AreEqual("Invalid input ILI file.", exception.Message);
    }

    [TestMethod]
    public void InputIliFileNotCorrectMapped()
    {
        using var process = IliValidatorProcessBuilder.Create()
            .InputFile("file")
            .OutputErrorLog("error_log")
            .OutputXtfLog("xtf_log")
            .InterlisCheckServiceBaseUrl("http://localhost/")
            .Build();
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processData = new ProcessData();
        processData.AddData("wrong_key", new ProcessDataPart(uploadFile));
        var exception = Assert.Throws<ArgumentException>(() => Task.Run(() => process.Run(processData)).GetAwaiter().GetResult());
        Assert.AreEqual("IliValidatorProcess: input data does not contain required key 'ili_file'.", exception.Message);
    }

    [TestMethod]
    public void UploadFailed()
    {
        using HttpResponseMessage uploadMockResponse = new()
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = JsonContent.Create(new InterlisUploadResponse()
            {
                JobId = jobId,
                StatusUrl = "/api/v1/status/" + jobId.ToString(),
            }),
        };
        using var process = IliValidatorProcessBuilder.Create()
            .InputFile("file")
            .OutputErrorLog("error_log")
            .OutputXtfLog("xtf_log")
            .InterlisCheckServiceBaseUrl("http://localhost/")
            .UploadMockResponse(uploadMockResponse)
            .Build();
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processData = new ProcessData();
        processData.AddData("file", new ProcessDataPart(uploadFile));
        var exception = Assert.Throws<ValidationFailedException>(() => Task.Run(() => process.Run(processData)).GetAwaiter().GetResult());
        Assert.AreEqual("Invalid transfer file", exception.Message);
    }

    private class IliValidatorProcessBuilder
    {
        private string inputFile;
        private string outputErrorLog;
        private string outputXtfLog;
        private string interlisCheckServiceBaseUrl;
        private string validationProfile = "DEFAULT";
        private int pollInterval = 500;
        private HttpResponseMessage uploadMockResponse;
        private HttpResponseMessage getStatusMockResponse;
        private HttpResponseMessage getAppLogMockResponse;
        private HttpResponseMessage getXtfLogMockResponse;

        public static IliValidatorProcessBuilder Create()
        {
            return new IliValidatorProcessBuilder();
        }

        public IliValidatorProcessBuilder InputFile(string inputFile)
        {
            this.inputFile = inputFile;
            return this;
        }

        public IliValidatorProcessBuilder OutputErrorLog(string outputErrorLog)
        {
            this.outputErrorLog = outputErrorLog;
            return this;
        }

        public IliValidatorProcessBuilder OutputXtfLog(string outputXtfLog)
        {
            this.outputXtfLog = outputXtfLog;
            return this;
        }

        public IliValidatorProcessBuilder InterlisCheckServiceBaseUrl(string interlisCheckServiceBaseUrl)
        {
            this.interlisCheckServiceBaseUrl = interlisCheckServiceBaseUrl;
            return this;
        }

        public IliValidatorProcessBuilder UploadMockResponse(HttpResponseMessage uploadMockResponse)
        {
            this.uploadMockResponse = uploadMockResponse;
            return this;
        }

        public IliValidatorProcessBuilder GetStatusMockResponse(HttpResponseMessage getStatusMockResponse)
        {
            this.getStatusMockResponse = getStatusMockResponse;
            return this;
        }

        public IliValidatorProcessBuilder GetAppLogMockResponse(HttpResponseMessage getAppLogMockResponse)
        {
            this.getAppLogMockResponse = getAppLogMockResponse;
            return this;
        }

        public IliValidatorProcessBuilder GetXtfLogMockResponse(HttpResponseMessage getXtfLogMockResponse)
        {
            this.getXtfLogMockResponse = getXtfLogMockResponse;
            return this;
        }

        public IliValidatorProcess Build()
        {
            var parameterization = new Api.Pipeline.Config.Parameterization()
            {
                { "profile", this.validationProfile },
                { "poll_interval", $"{this.pollInterval}" },
            };
            var dataHandlingConfig = new Api.Pipeline.Config.DataHandlingConfig()
            {
                InputMapping = new Dictionary<string, string>()
                {
                    { "ili_file", this.inputFile },
                },
                OutputMapping = new Dictionary<string, string>()
                {
                    { "error_log", this.outputErrorLog },
                    { "xtf_log", this.outputXtfLog },
                },
            };

            var inMemorySettings = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Validation:InterlisCheckServiceUrl", this.interlisCheckServiceBaseUrl),
            };

            #pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
            #pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
            var process = new IliValidatorProcess();
            process.Initialize(parameterization, dataHandlingConfig, configuration, CancellationToken.None);

            var interlisValidatorMessageHandlerMock = new Mock<HttpMessageHandler>();
            interlisValidatorMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/upload"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(this.uploadMockResponse);
            interlisValidatorMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/status/" + jobId.ToString()),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(this.getStatusMockResponse);
            interlisValidatorMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=log"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(this.getAppLogMockResponse);
            interlisValidatorMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri != null && req.RequestUri.ToString() == interlisCheckServiceBaseUrl + "api/v1/download?jobId=" + jobId.ToString() + "&logType=xtf"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(this.getXtfLogMockResponse);
            #pragma warning disable CA2000 // Dispose objects before losing scope
            var httpClient = new HttpClient(interlisValidatorMessageHandlerMock.Object) { BaseAddress = new Uri(interlisCheckServiceBaseUrl) };
            #pragma warning restore CA2000 // Dispose objects before losing scope
            typeof(IliValidatorProcess)
                ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(process, httpClient);
            return process;
        }
    }
}
