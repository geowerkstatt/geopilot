using GeoCop.Api.Authorization;
using GeoCop.Api.Contracts;
using GeoCop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GeoCop.Api.Controllers;

/// <summary>
/// Controller for user information.
/// </summary>
[Authorize(Policy = GeocopPolicies.User)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class UserController : ControllerBase
{
    private readonly Context context;
    private readonly BrowserAuthOptions authOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="authOptions">The browser auth options.</param>
    public UserController(Context context, IOptions<BrowserAuthOptions> authOptions)
    {
        this.context = context;
        this.authOptions = authOptions.Value;
    }

    /// <summary>
    /// Gets the current user information.
    /// </summary>
    /// <returns>The <see cref="User"/> that is currently logged in.</returns>
    [HttpGet]
    public async Task<User?> GetAsync()
    {
        return await context.GetUserByPrincipalAsync(User);
    }

    /// <summary>
    /// Gets the specified auth options.
    /// </summary>
    /// <returns>The configured options used for authentication.</returns>
    [HttpGet("auth")]
    [AllowAnonymous]
    public BrowserAuthOptions GetAuthOptions()
    {
        return authOptions;
    }
}
