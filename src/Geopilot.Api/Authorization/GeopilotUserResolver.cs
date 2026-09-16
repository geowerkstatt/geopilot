using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Resolves the current request to its <see cref="User"/> through the identity provider's user info, creating
/// the account on first sign-in and refreshing name and email otherwise. Scoped, like the user info service it
/// depends on, and shared by every authorization handler that has to know the person behind a token.
/// </summary>
public class GeopilotUserResolver : IGeopilotUserResolver
{
    private readonly Context dbContext;
    private readonly IGeopilotUserInfoService userInfoService;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<GeopilotUserResolver> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserResolver"/> class.
    /// </summary>
    public GeopilotUserResolver(
        Context dbContext,
        IGeopilotUserInfoService userInfoService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GeopilotUserResolver> logger)
    {
        this.dbContext = dbContext;
        this.userInfoService = userInfoService;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<User?> ResolveAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var accessToken = ExtractAccessToken(httpContext);
        if (httpContext is null || string.IsNullOrEmpty(accessToken))
        {
            logger.LogError("No access token found in request.");
            return null;
        }

        // A registered machine client is never a user, whatever the identity provider would answer for its
        // token. This is what keeps client credentials out of the web interface, and it must not depend on
        // the identity provider refusing user info for a machine.
        var subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (subject is not null && await dbContext.MachineClients.AnyAsync(c => c.AuthIdentifier == subject))
        {
            logger.LogWarning("Subject <{Sub}> is a registered machine client and cannot act as a user.", subject);
            return null;
        }

        UserInfoResponse? userInfo;
        try
        {
            userInfo = await userInfoService.GetUserInfoAsync(accessToken, httpContext.RequestAborted);
        }
        catch (IdentityProviderUnavailableException ex)
        {
            logger.LogError(ex, "User info request failed.");
            return null;
        }

        if (userInfo == null)
            return null;

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.AuthIdentifier == userInfo.Sub);
        if (user == null)
        {
            user = new User
            {
                AuthIdentifier = userInfo.Sub,
                Email = userInfo.Email,
                FullName = userInfo.Name,
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("New user (with sub <{Sub}>) has been registered in database.", userInfo.Sub);
        }
        else if (user.Email != userInfo.Email || user.FullName != userInfo.Name)
        {
            // Update user information in database from userinfo response
            user.Email = userInfo.Email;
            user.FullName = userInfo.Name;
            await dbContext.SaveChangesAsync();
        }

        return user;
    }

    private static string? ExtractAccessToken(HttpContext? httpContext)
    {
        if (httpContext is null) return null;

        var cookieToken = httpContext.Request.Cookies[AuthDefaults.AuthCookieName];
        if (!string.IsNullOrEmpty(cookieToken))
        {
            return cookieToken;
        }

        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        return null;
    }
}
