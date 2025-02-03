using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

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
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of organisations.", typeof(IEnumerable<Organisation>), "application/json")]
    public List<Organisation> Get()
    {
        logger.LogInformation("Getting organisations.");

        return context.OrganisationsWithIncludes
            .AsNoTracking()
            .ToList();
    }

    /// <summary>
    /// Gets the organisation with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>The organisation.</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the organisation", typeof(Organisation), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to get individual organisations.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The organisation could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> GetById(int id)
    {
        logger.LogInformation($"Getting organisation with id <{id}>.");

        var organisation = await context.OrganisationsWithIncludes.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id).ConfigureAwait(false);

        if (organisation == null)
        {
            return NotFound();
        }

        return Ok(organisation);
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="organisation"/> specified.
    /// </summary>
    /// <param name="organisation">The organisation to create.</param>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The organisation was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(Organisation organisation)
    {
        try
        {
            if (organisation == null)
                return BadRequest();

            var mandateIds = organisation.Mandates.Select(m => m.Id).ToList();
            organisation.Mandates = await context.Mandates
                .Where(m => mandateIds.Contains(m.Id))
                .ToListAsync();

            var userIds = organisation.Users.Select(u => u.Id).ToList();
            organisation.Users = await context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var entityEntry = await context.AddAsync(organisation).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.OrganisationsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == entityEntry.Entity.Id);
            if (result == default)
                return Problem("Unable to retrieve created organisation.");

            var location = new Uri(string.Format(CultureInfo.InvariantCulture, $"/api/v1/organisation/{result.Id}"), UriKind.Relative);
            return Created(location, result);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occurred while creating the organisation.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="organisation"/> specified.
    /// </summary>
    /// <param name="organisation">The organisation to update.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The organisation was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The organisation could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]

    public async Task<IActionResult> Edit(Organisation organisation)
    {
        try
        {
            if (organisation == null)
                return BadRequest();

            var existingOrganisation = await context.OrganisationsWithIncludes
                .FirstOrDefaultAsync(o => o.Id == organisation.Id);

            if (existingOrganisation == null)
                return NotFound();

            context.Entry(existingOrganisation).CurrentValues.SetValues(organisation);

            var mandateIds = organisation.Mandates.Select(m => m.Id).ToList();
            var mandates = await context.Mandates
                .Where(m => mandateIds.Contains(m.Id))
                .ToListAsync();
            existingOrganisation.Mandates.Clear();
            foreach (var mandate in mandates)
            {
                if (!existingOrganisation.Mandates.Contains(mandate))
                    existingOrganisation.Mandates.Add(mandate);
            }

            var userIds = organisation.Users.Select(u => u.Id).ToList();
            var users = await context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
            existingOrganisation.Users.Clear();
            foreach (var user in users)
            {
                if (!existingOrganisation.Users.Contains(user))
                    existingOrganisation.Users.Add(user);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.OrganisationsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == organisation.Id);

            if (result == default)
                return Problem("Unable to retrieve updated organisation.");

            return Ok(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occurred while updating the organisation.");
            return Problem(e.Message);
        }
    }
}
