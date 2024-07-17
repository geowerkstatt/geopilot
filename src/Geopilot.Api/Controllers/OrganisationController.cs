using Geopilot.Api.Authorization;
using Geopilot.Api.DTOs;
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
public class OrganisationController : ControllerBase
{
    private readonly ILogger<OrganisationController> logger;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganisationController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting organisations.</param>
    public OrganisationController(ILogger<OrganisationController> logger, Context context)
    {
        this.logger = logger;
        this.context = context;
    }

    /// <summary>
    /// Get a list of organisations.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of organisations.", typeof(IEnumerable<Organisation>), new[] { "application/json" })]
    public IActionResult Get()
    {
        logger.LogInformation("Getting organisations.");

        var organisations = context.Organisations
            .Include(o => o.Mandates)
            .Include(o => o.Users)
            .AsNoTracking()
            .Select(OrganisationDto.FromOrganisation)
            .ToList();

        return Ok(organisations);
    }
}
