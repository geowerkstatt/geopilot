using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GeoCop.Api.Authorization;

/// <summary>
/// Middleware that automatically authorizes as the first user in development.
/// </summary>
public class DevelopmentAuthorizationMiddleware
{
    private readonly RequestDelegate next;
    private readonly IWebHostEnvironment env;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevelopmentAuthorizationMiddleware"/> class.
    /// </summary>
    /// <param name="next">Delegate to the next middleware.</param>
    /// <param name="env">The current host environment.</param>
    public DevelopmentAuthorizationMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        this.next = next;
        this.env = env;
    }

    /// <summary>
    /// Runs the middleware.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <param name="dbContext">The database context.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, Context dbContext)
    {
        if (env.IsDevelopment() && context.User.Identity?.IsAuthenticated == true)
        {
            // Automatically authorize as the first user in development
            var firstDbUser = await dbContext.Users
                .OrderBy(user => user.Id)
                .FirstAsync();
            var claims = context.User.Claims
                .Where(claim => claim.Type != ContextExtensions.UserIdClaim)
                .Append(new Claim(ContextExtensions.UserIdClaim, firstDbUser.AuthIdentifier));

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, context.User.Identity.AuthenticationType));
        }

        await next(context);
    }
}
