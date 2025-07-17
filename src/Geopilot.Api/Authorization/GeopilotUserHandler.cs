using Geopilot.Api.Models;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization handler for <see cref="GeopilotUserRequirement"/>.
/// </summary>
public class GeopilotUserHandler : AuthorizationHandler<GeopilotUserRequirement>
{
    private readonly Context dbContext;
    private readonly ILogger<GeopilotUserHandler> logger;
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly IHttpContextAccessor httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserHandler"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger used for authorization related logging.</param>
    /// <param name="httpClient">HTTP client for userinfo endpoint calls.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="httpContextAccessor">HTTP context accessor.</param>
    public GeopilotUserHandler(
        ILogger<GeopilotUserHandler> logger,
        Context dbContext,
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GeopilotUserRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var user = await UpdateOrCreateUser(context);
        if (user is null)
            return;

        if (requirement.RequireAdmin && !user.IsAdmin)
        {
            logger.LogWarning("User with id <{UserId}> did not fulfill admin requirement.", user.AuthIdentifier);
            return;
        }

        context.Succeed(requirement);
    }

    internal async Task<User?> UpdateOrCreateUser(AuthorizationHandlerContext context)
    {
        var accessToken = ExtractAccessToken(context);
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogError("No access token found in request.");
            return null;
        }

        var userInfo = await GetUserInfoAsync(accessToken);
        if (userInfo == null)
            return null;

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == userInfo.Sub);
        if (user == null)
        {
            user = new User
            {
                AuthIdentifier = userInfo.Sub,
                Email = userInfo.Email,
                FullName = userInfo.Name
            };

            // Elevate first user to admin
            if (!dbContext.Users.Any())
            {
                user.IsAdmin = true;
            }

            await dbContext.Users.AddAsync(user);
            logger.LogInformation("New user (with sub <{Sub}>) has been registered in database.", userInfo.Sub);
        }
        else if (user.Email != userInfo.Email || user.FullName != userInfo.Name)
        {
            // Update user information in database from userinfo response
            user.Email = userInfo.Email;
            user.FullName = userInfo.Name;
        }

        await dbContext.SaveChangesAsync();
        return await dbContext.Users.SingleAsync(u => u.AuthIdentifier == userInfo.Sub);
    }

    private string? ExtractAccessToken(AuthorizationHandlerContext context)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Check Authorization header first
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            return authHeader.Substring("Bearer ".Length);
        }

        // Fallback to cookie
        return httpContext.Request.Cookies["geopilot.auth"];
    }

    private async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken)
    {
        try
        {
            var userInfoEndpoint = configuration["Auth:UserInfoUrl"];

            using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to retrieve user info. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (string.IsNullOrEmpty(userInfo?.Sub) || string.IsNullOrEmpty(userInfo?.Email) || string.IsNullOrEmpty(userInfo?.Name))
            {
                logger.LogError("UserInfo response missing required fields (sub, email, name).");
                return null;
            }

            return userInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user info from userinfo endpoint.");
            return null;
        }
    }
}

