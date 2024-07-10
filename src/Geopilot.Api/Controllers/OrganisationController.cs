using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for organisations.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class OrganisationController : BaseController<Organisation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrganisationController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting organisations.</param>
    public OrganisationController(ILogger<Organisation> logger, Context context)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Get a list of organisations.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of organisations.", typeof(IEnumerable<Organisation>), new[] { "application/json" })]
    public async Task<IActionResult> Get()
    {
        Logger.LogInformation("Getting organisations.");

        var organisations = await Context.Organisations
            .Include(o => o.Mandates)
            .Include(o => o.Users)
            .AsNoTracking()
            .ToListAsync();
        return Ok(organisations);
    }

    /// <inheritdoc/>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The organisation was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]
    public override async Task<IActionResult> Create(Organisation entity)
    {
        return await base.Create(entity);
    }

    /// <inheritdoc/>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The organisation was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The organisation could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]

    public override async Task<IActionResult> Edit(Organisation entity)
    {
        return await base.Edit(entity);
    }
}
