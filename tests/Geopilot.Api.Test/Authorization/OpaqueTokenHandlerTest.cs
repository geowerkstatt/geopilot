using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Moq;
using Moq.Protected;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;

namespace Geopilot.Api.Test.Authorization;

[TestClass]
public class OpaqueTokenHandlerTest
{
    private Mock<HttpMessageHandler> httpHandlerMock;
    private Mock<IHttpClientFactory> httpClientFactoryMock;
    private Mock<IGeopilotUserInfoService> userInfoServiceMock;
    private HttpClient? httpClient;
    private OpaqueTokenOptions options;

    [TestInitialize]
    public void Initialize()
    {
        httpHandlerMock = new Mock<HttpMessageHandler>();
        httpClient = new HttpClient(httpHandlerMock.Object);

        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(OpaqueTokenHandler.HttpClientName)).Returns(httpClient);

        userInfoServiceMock = new Mock<IGeopilotUserInfoService>();

        options = new OpaqueTokenOptions
        {
            IntrospectionUrl = "https://idp.example.com/introspect",
            ConfidentialClientId = "geopilot-client",
            ConfidentialClientSecret = "secret123",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
            Audience = "geopilot-api",
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        httpClient?.Dispose();
    }

    [TestMethod]
    public async Task AuthenticateAsyncWithoutTokenReturnsNoResult()
    {
        var context = new DefaultHttpContext();
        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.None);
    }

    [TestMethod]
    public async Task AuthenticateAsyncCookieWinsOverHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(HeaderNames.Cookie, $"{AuthDefaults.AuthCookieName}=cookie-token");
        context.Request.Headers.Append(HeaderNames.Authorization, "Bearer header-token");

        string? capturedBody = null;
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            })
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":false}", Encoding.UTF8, "application/json"),
            });

        await RunAuthenticateAsync(context);

        Assert.IsNotNull(capturedBody);
        StringAssert.Contains(capturedBody, "token=cookie-token");
        Assert.DoesNotContain("header-token", capturedBody);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveFalseReturnsFailAndDoesNotCallUserInfo()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":false}", Encoding.UTF8, "application/json"),
            });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsFalse(result.Succeeded);
        userInfoServiceMock.Verify(s => s.GetUserInfoAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task AuthenticateAsyncHttp500ReturnsFail()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncInvalidJsonReturnsFail()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ not valid json }", Encoding.UTF8, "application/json"),
            });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueWithoutAudAndSubReturnsSuccessWithUserInfoSub()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync(new UserInfoResponse
        {
            Sub = "user-42",
            Email = "user42@example.com",
            Name = "User Forty Two",
        });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("user-42", result.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueWithDifferentSubUsesUserInfoSub()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true,\"sub\":\"idp-sub\"}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync(new UserInfoResponse
        {
            Sub = "userinfo-sub",
            Email = "userinfo@example.com",
            Name = "UserInfo User",
        });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("userinfo-sub", result.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueWhenUserInfoReturnsNullReturnsFail()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync((UserInfoResponse?)null);

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueMatchingAudStringReturnsSuccess()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true,\"aud\":\"geopilot-api\"}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync(new UserInfoResponse
        {
            Sub = "user-1",
            Email = "u1@example.com",
            Name = "User One",
        });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueMatchingAudArrayReturnsSuccess()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true,\"aud\":[\"other-api\",\"geopilot-api\"]}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync(new UserInfoResponse
        {
            Sub = "user-1",
            Email = "u1@example.com",
            Name = "User One",
        });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueNonMatchingAudReturnsFail()
    {
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true,\"aud\":\"different-api\"}", Encoding.UTF8, "application/json"),
            });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncClientSecretBasicSendsBasicHeaderAndNoSecretInBody()
    {
        options.IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic;
        var context = CreateContextWithBearerToken("opaque-token");

        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        AuthenticationHeaderValue? capturedAuth = null;

        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedMethod = req.Method;
                capturedAuth = req.Headers.Authorization;
                capturedBody = req.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            })
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":false}", Encoding.UTF8, "application/json"),
            });

        await RunAuthenticateAsync(context);

        Assert.IsNotNull(capturedBody);
        Assert.AreEqual(HttpMethod.Post, capturedMethod);
        StringAssert.Contains(capturedBody, "token=opaque-token");
        StringAssert.Contains(capturedBody, "token_type_hint=access_token");
        Assert.DoesNotContain("client_secret", capturedBody);
        Assert.AreEqual("Basic", capturedAuth?.Scheme);

        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString("geopilot-client")}:{Uri.EscapeDataString("secret123")}"));
        Assert.AreEqual(expectedCredentials, capturedAuth?.Parameter);
    }

    [TestMethod]
    public async Task AuthenticateAsyncClientSecretPostSendsSecretInBodyAndNoAuthorizationHeader()
    {
        options.IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretPost;
        var context = CreateContextWithBearerToken("opaque-token");

        string? capturedBody = null;
        AuthenticationHeaderValue? capturedAuth = null;

        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedAuth = req.Headers.Authorization;
                capturedBody = req.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult();
            })
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":false}", Encoding.UTF8, "application/json"),
            });

        await RunAuthenticateAsync(context);

        Assert.IsNotNull(capturedBody);
        Assert.IsNull(capturedAuth);
        StringAssert.Contains(capturedBody, "token=opaque-token");
        StringAssert.Contains(capturedBody, "token_type_hint=access_token");
        StringAssert.Contains(capturedBody, "client_id=geopilot-client");
        StringAssert.Contains(capturedBody, "client_secret=secret123");
    }

    [TestMethod]
    public async Task HandleChallengeAsyncReturns401WithWwwAuthenticateHeader()
    {
        var context = new DefaultHttpContext();
        var (handler, _) = await RunAuthenticateAsync(context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.AreEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.AreEqual("Bearer", context.Response.Headers[HeaderNames.WWWAuthenticate].ToString());
    }

    [TestMethod]
    public async Task AddGeopilotAuthenticationWithoutFormatDefaultsToJwt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Authority"] = "https://idp.example.com",
            ["Auth:Audience"] = "geopilot-api",
        });

        var format = builder.AddGeopilotAuthentication();

        Assert.AreEqual(AccessTokenFormat.Jwt, format);

        using var app = builder.Build();
        var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();

        Assert.IsNotNull(defaultScheme);
        Assert.AreEqual(JwtBearerDefaults.AuthenticationScheme, defaultScheme.Name);
        Assert.AreEqual(typeof(JwtBearerHandler), defaultScheme.HandlerType);
    }

    [TestMethod]
    public async Task AddGeopilotAuthenticationWithOpaqueFormatRegistersOpaqueScheme()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:AccessTokenFormat"] = "Opaque",
            ["Auth:IntrospectionUrl"] = "https://idp.example.com/introspect",
            ["Auth:ConfidentialClientId"] = "client-id",
            ["Auth:ConfidentialClientSecret"] = "secret",
            ["Auth:IntrospectionAuthMethod"] = "ClientSecretBasic",
            ["Auth:UserInfoUrl"] = "https://idp.example.com/userinfo",
        });

        var format = builder.AddGeopilotAuthentication();

        Assert.AreEqual(AccessTokenFormat.Opaque, format);

        using var app = builder.Build();
        var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();

        Assert.IsNotNull(defaultScheme);
        Assert.AreEqual("Bearer", defaultScheme.Name);
        Assert.AreEqual(typeof(OpaqueTokenHandler), defaultScheme.HandlerType);
    }

    [TestMethod]
    public async Task AddGeopilotAuthenticationWithInvalidOpaqueConfigThrowsOnStart()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = "http://127.0.0.1:0",
            ["Auth:AccessTokenFormat"] = "Opaque",
        });

        builder.AddGeopilotAuthentication();

        using var app = builder.Build();
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => app.StartAsync());
        Assert.AreEqual("Auth:IntrospectionUrl is required.", ex.Message);
    }

    [TestMethod]
    public async Task AuthenticateAsyncActiveTrueWithEmptyAudienceIgnoresTokenAudience()
    {
        options.Audience = string.Empty;
        var context = CreateContextWithBearerToken("opaque-token");
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"active\":true,\"aud\":\"different-api\"}", Encoding.UTF8, "application/json"),
            });

        userInfoServiceMock.Setup(s => s.GetUserInfoAsync("opaque-token")).ReturnsAsync(new UserInfoResponse
        {
            Sub = "user-1",
            Email = "u1@example.com",
            Name = "User One",
        });

        var (_, result) = await RunAuthenticateAsync(context);

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task AuthenticateAsyncUnsupportedAuthMethodThrowsInvalidOperationException()
    {
        options.IntrospectionAuthMethod = (IntrospectionAuthMethod)999;
        var context = CreateContextWithBearerToken("opaque-token");

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => RunAuthenticateAsync(context));
        Assert.AreEqual("Unsupported introspection authentication method: 999.", ex.Message);
    }

    [TestMethod]
    public void ValidateWithValidOptionsSucceeds()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "https://idp.example.com/introspect",
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        testOptions.Validate();
    }

    [TestMethod]
    public void ValidateWithoutIntrospectionUrlThrows()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = " ",
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => testOptions.Validate());
        Assert.AreEqual("Auth:IntrospectionUrl is required.", ex.Message);
    }

    [TestMethod]
    public void ValidateWithHttpUrlAndRequireHttpsMetadataThrows()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "http://idp.example.com/introspect",
            RequireHttpsMetadata = true,
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => testOptions.Validate());
        Assert.AreEqual("Auth:IntrospectionUrl must use HTTPS when RequireHttpsMetadata is enabled.", ex.Message);
    }

    [TestMethod]
    public void ValidateWithHttpUrlAndRequireHttpsMetadataDisabledSucceeds()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "http://idp.example.com/introspect",
            RequireHttpsMetadata = false,
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        testOptions.Validate();
    }

    [TestMethod]
    public void ValidateWithoutConfidentialClientIdThrows()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "https://idp.example.com/introspect",
            ConfidentialClientId = "",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => testOptions.Validate());
        Assert.AreEqual("Auth:ConfidentialClientId is required.", ex.Message);
    }

    [TestMethod]
    public void ValidateWithoutIntrospectionAuthMethodThrows()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "https://idp.example.com/introspect",
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = "client-secret",
            IntrospectionAuthMethod = null,
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => testOptions.Validate());
        Assert.AreEqual("Auth:IntrospectionAuthMethod is required.", ex.Message);
    }

    [TestMethod]
    public void ValidateWithoutConfidentialClientSecretThrows()
    {
        var testOptions = new OpaqueTokenOptions
        {
            IntrospectionUrl = "https://idp.example.com/introspect",
            ConfidentialClientId = "client-id",
            ConfidentialClientSecret = " ",
            IntrospectionAuthMethod = IntrospectionAuthMethod.ClientSecretBasic,
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => testOptions.Validate());
        Assert.AreEqual("Auth:ConfidentialClientSecret is required.", ex.Message);
    }

    private static DefaultHttpContext CreateContextWithBearerToken(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(HeaderNames.Authorization, $"Bearer {token}");
        return context;
    }

    private async Task<(OpaqueTokenHandler Handler, AuthenticateResult Result)> RunAuthenticateAsync(HttpContext context)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<OpaqueTokenOptions>>();
        optionsMonitor.Setup(m => m.Get(It.IsAny<string>())).Returns(options);
        optionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var handler = new OpaqueTokenHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            httpClientFactoryMock.Object,
            userInfoServiceMock.Object);

        var scheme = new AuthenticationScheme("Bearer", "Bearer", typeof(OpaqueTokenHandler));
        await handler.InitializeAsync(scheme, context);
        var result = await handler.AuthenticateAsync();
        return (handler, result);
    }
}
