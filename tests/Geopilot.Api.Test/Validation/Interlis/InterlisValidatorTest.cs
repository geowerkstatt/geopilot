using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation.Interlis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace Geopilot.Api.Test.Validation.Interlis;

[TestClass]
public class InterlisValidatorTest
{
    private Mock<ILogger<InterlisValidator>> loggerMock;
    private Mock<IFileProvider> fileProviderMock;
    private Mock<HttpMessageHandler> messageHandlerMock;
    private HttpClient httpClient;

    private InterlisValidator interlisValidator;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<InterlisValidator>>();
        fileProviderMock = new Mock<IFileProvider>();
        messageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpClient = new HttpClient(messageHandlerMock.Object);
        httpClient.BaseAddress = new Uri("https://localhost");
        interlisValidator = new InterlisValidator(
            loggerMock.Object,
            httpClient,
            Options.Create(new JsonOptions()));
    }

    [TestMethod]
    public async Task GetSupportedProfilesAsyncReturnsProfilesOnSuccess()
    {
        var profiles = new List<Profile>
        {
            new() { Id = "p1", Titles = new() { new() { Language = "en", Text = "Profile One" } } },
            new() { Id = "p2" },
        };

        using HttpResponseMessage mockResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(profiles),
        };

        messageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        var result = await interlisValidator.GetSupportedProfilesAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("p1", result[0].Id);
        Assert.AreEqual("p2", result[1].Id);
    }

    [TestMethod]
    public async Task GetSupportedProfilesAsyncReturnsEmptyOnFailure()
    {
        using HttpResponseMessage errorResponse = new()
        {
            StatusCode = HttpStatusCode.InternalServerError,
        };

        messageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Get),
            ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(errorResponse);

        var result = await interlisValidator.GetSupportedProfilesAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }
}
