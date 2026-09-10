using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Geopilot.Api.Test.Authorization;

[TestClass]
public class GeopilotUserInfoServiceTest
{
    private Mock<HttpMessageHandler> httpMessageHandlerMock;
    private Mock<IConfiguration> configurationMock;
    private Mock<ILogger<GeopilotUserInfoService>> loggerMock;
    private HttpClient httpClient;
    private GeopilotUserInfoService userInfoService;

    [TestInitialize]
    public void Initialize()
    {
        httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        configurationMock = new Mock<IConfiguration>();
        loggerMock = new Mock<ILogger<GeopilotUserInfoService>>();

        httpClient = new HttpClient(httpMessageHandlerMock.Object);

        // Setup configuration
        configurationMock.Setup(x => x["Auth:UserInfoUrl"])
            .Returns("https://example.com/userinfo");

        userInfoService = new GeopilotUserInfoService(httpClient, configurationMock.Object, loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        httpClient?.Dispose();
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithValidResponseReturnsUserInfo()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var userInfoResponse = new UserInfoResponse
        {
            Sub = "user123",
            Email = "test@example.com",
            Name = "Test User",
        };

        var jsonResponse = JsonSerializer.Serialize(userInfoResponse);
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == "https://example.com/userinfo" &&
                    req.Headers.Authorization != null &&
                    req.Headers.Authorization.Scheme == "Bearer" &&
                    req.Headers.Authorization.Parameter == accessToken),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("user123", result.Sub);
        Assert.AreEqual("test@example.com", result.Email);
        Assert.AreEqual("Test User", result.Name);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithUnsuccessfulResponseReturnsNull()
    {
        // Arrange
        var accessToken = "invalid-access-token";
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve user info. Status: Unauthorized")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithMissingSubReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var invalidUserInfo = new { Email = "test@example.com", Name = "Test User" }; // Missing Sub

        var jsonResponse = JsonSerializer.Serialize(invalidUserInfo);
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserInfo response missing required fields")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithMissingEmailReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var invalidUserInfo = new { Sub = "user123", Name = "Test User" }; // Missing Email

        var jsonResponse = JsonSerializer.Serialize(invalidUserInfo);
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserInfo response missing required fields")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithMissingNameReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var invalidUserInfo = new { Sub = "user123", Email = "test@example.com" }; // Missing Name

        var jsonResponse = JsonSerializer.Serialize(invalidUserInfo);
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserInfo response missing required fields")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithInvalidJsonReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var invalidJsonResponse = "{ invalid json }";

        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(invalidJsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving user info")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithHttpExceptionReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error retrieving user info")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithEmptyStringFieldsReturnsNull()
    {
        // Arrange
        var accessToken = "valid-access-token";
        var userInfoResponse = new UserInfoResponse
        {
            Sub = "",  // Empty string
            Email = "test@example.com",
            Name = "Test User",
        };

        var jsonResponse = JsonSerializer.Serialize(userInfoResponse);
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
        };

        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNull(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UserInfo response missing required fields")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithSameTokenHitsHttpEndpointOnce()
    {
        // Arrange
        var accessToken = "same-access-token";
        var userInfoResponse = new UserInfoResponse
        {
            Sub = "user123",
            Email = "test@example.com",
            Name = "Test User",
        };

        var jsonResponse = JsonSerializer.Serialize(userInfoResponse);
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
            });

        // Act
        var firstResult = await userInfoService.GetUserInfoAsync(accessToken);
        var secondResult = await userInfoService.GetUserInfoAsync(accessToken);

        // Assert
        Assert.IsNotNull(firstResult);
        Assert.IsNotNull(secondResult);
        Assert.AreSame(firstResult, secondResult);
        httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task GetUserInfoAsyncWithDifferentTokensHitsHttpEndpointTwice()
    {
        // Arrange
        var token1 = "token-1";
        var token2 = "token-2";
        var userInfoResponse1 = new UserInfoResponse
        {
            Sub = "user1",
            Email = "user1@example.com",
            Name = "User One",
        };
        var userInfoResponse2 = new UserInfoResponse
        {
            Sub = "user2",
            Email = "user2@example.com",
            Name = "User Two",
        };

        httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(userInfoResponse1), Encoding.UTF8, "application/json"),
            })
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(userInfoResponse2), Encoding.UTF8, "application/json"),
            });

        // Act
        var firstResult = await userInfoService.GetUserInfoAsync(token1);
        var secondResult = await userInfoService.GetUserInfoAsync(token2);

        // Assert
        Assert.IsNotNull(firstResult);
        Assert.IsNotNull(secondResult);
        Assert.AreEqual("user1", firstResult.Sub);
        Assert.AreEqual("user2", secondResult.Sub);
        httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
