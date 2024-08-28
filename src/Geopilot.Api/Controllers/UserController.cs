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
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of users.", typeof(IEnumerable<User>), "application/json")]
    public List<User> Get()
    {
        logger.LogInformation("Getting users.");

        return context.UsersWithIncludes
            .AsNoTracking()
            .ToList();
    }

    /// <summary>
    /// Gets the current user information.
    /// </summary>
    /// <returns>The <see cref="User"/> that is currently logged in.</returns>
    [HttpGet("self")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the currently logged in user.", typeof(User), "application/json")]
    public async Task<User?> GetSelfAsync()
    {
        var user = await context.GetUserByPrincipalAsync(User);
        logger.LogTrace("User <{AuthIdenifier}> getting account information.", user.AuthIdentifier);
        return user;
    }

    /// <summary>
    /// Get a user with the specified <paramref name="id"/>.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the user with the specified id.", typeof(User), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The user could not be found.")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await context.UsersWithIncludes
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id);

        if (user == default)
            return NotFound();

        return Ok(user);
    }

    /// <summary>
    /// Gets the specified auth options.
    /// </summary>
    /// <returns>The configured options used for authentication.</returns>
    [HttpGet("auth")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the auth configuration used by the server for token validation.", typeof(BrowserAuthOptions), "application/json")]
    public BrowserAuthOptions GetAuthOptions()
    {
        logger.LogInformation("Getting auth options.");
        return authOptions;
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="user"/> specified.
    /// </summary>
    /// <param name="user">The user to update.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the updated user.", typeof(User), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The user could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The user could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to update the user.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The user could not be updated due to an internal server error.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Edit(User user)
    {
        try
        {
            if (user == null)
                return BadRequest();

            var existingUser = await context.UsersWithIncludes.SingleOrDefaultAsync(u => u.Id == user.Id);

            if (existingUser == null)
                return NotFound();

            existingUser.IsAdmin = user.IsAdmin;

            var organisationIds = user.Organisations.Select(o => o.Id).ToList();
            var organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();
            existingUser.Organisations.Clear();
            foreach (var organisation in organisations)
            {
                existingUser.Organisations.Add(organisation);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.UsersWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            return Ok(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the user.");
            return Problem(e.Message);
        }
    }
}
