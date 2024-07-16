using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for user information.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> logger;
    private readonly Context context;
    private readonly BrowserAuthOptions authOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="logger">The logger for the instance.</param>
    /// <param name="context">The database context.</param>
    /// <param name="authOptions">The browser auth options.</param>
    public UserController(ILogger<UserController> logger, Context context, IOptions<BrowserAuthOptions> authOptions)
    {
        this.logger = logger;
        this.context = context;
        this.authOptions = authOptions.Value;
    }

    /// <summary>
    /// Gets a list of users.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of users.", typeof(IEnumerable<User>), new[] { "application/json" })]
    public List<User> Get()
    {
        logger.LogInformation("Getting users.");

        return context.Users
            .Include(u => u.Organisations)
            .AsNoTracking()
            .ToList();
    }

    /// <summary>
    /// Gets the current user information.
    /// </summary>
    /// <returns>The <see cref="User"/> that is currently logged in.</returns>
    [HttpGet("self")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the currently logged in user.", typeof(User), new[] { "application/json" })]
    public async Task<User?> GetSelfAsync()
    {
        var user = await context.GetUserByPrincipalAsync(User);
        if (user == null)
        {
            logger.LogWarning("Getting user information attempted without registered user with name <{UserName}>", User.Identity?.Name);
        }
        else
        {
            logger.LogTrace("User <{AuthIdenifier}> getting account information.", user.AuthIdentifier);
        }

        return user;
    }

    /// <summary>
    /// Gets the specified auth options.
    /// </summary>
    /// <returns>The configured options used for authentication.</returns>
    [HttpGet("auth")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the auth configuration used by the server for token validation.", typeof(BrowserAuthOptions), new[] { "application/json" })]
    public BrowserAuthOptions GetAuthOptions()
    {
        logger.LogInformation("Getting auth options.");
        return authOptions;
    }
}
