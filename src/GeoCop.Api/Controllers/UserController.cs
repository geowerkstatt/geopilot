using GeoCop.Api.Authorization;
using GeoCop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="UserController"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public UserController(Context context)
    {
        this.context = context;
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
}
