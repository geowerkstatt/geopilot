using Geopilot.Api.Authorization;
using Geopilot.Api.DTOs;
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
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of organisations.", typeof(IEnumerable<Organisation>), new[] { "application/json" })]
    public List<OrganisationDto> Get()
    {
        logger.LogInformation("Getting organisations.");

        return context.OrganisationsWithIncludes
            .AsNoTracking()
            .Select(OrganisationDto.FromOrganisation)
            .ToList();
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="organisationDto"/> specified.
    /// </summary>
    /// <param name="organisationDto">The organisation to create.</param>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The organisation was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]
    public async Task<IActionResult> Create(OrganisationDto organisationDto)
    {
        try
        {
            if (organisationDto == null)
                return BadRequest();

            var organisation = await TransformToOrganisation(organisationDto);

            var entityEntry = await context.AddAsync(organisation).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.OrganisationsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == entityEntry.Entity.Id);
            if (result == default)
                return Problem("Unable to retrieve created organisation.");

            var location = new Uri(string.Format(CultureInfo.InvariantCulture, $"/api/v1/organisation/{result.Id}"), UriKind.Relative);
            return Created(location, OrganisationDto.FromOrganisation(result));
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occurred while creating the organisation.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="organisationDto"/> specified.
    /// </summary>
    /// <param name="organisationDto">The organisation to update.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The organisation was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The organisation could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The organisation could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit an organisation.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]

    public async Task<IActionResult> Edit(OrganisationDto organisationDto)
    {
        try
        {
            if (organisationDto == null)
                return BadRequest();

            var updatedOrganisation = await TransformToOrganisation(organisationDto);
            var existingOrganisation = await context.OrganisationsWithIncludes
                .FirstOrDefaultAsync(o => o.Id == organisationDto.Id);

            if (existingOrganisation == null)
                return NotFound();

            context.Entry(existingOrganisation).CurrentValues.SetValues(updatedOrganisation);

            existingOrganisation.Mandates.Clear();
            foreach (var mandate in updatedOrganisation.Mandates)
            {
                if (!existingOrganisation.Mandates.Contains(mandate))
                    existingOrganisation.Mandates.Add(mandate);
            }

            existingOrganisation.Users.Clear();
            foreach (var user in updatedOrganisation.Users)
            {
                if (!existingOrganisation.Users.Contains(user))
                    existingOrganisation.Users.Add(user);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.OrganisationsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == organisationDto.Id);

            if (result == default)
                return Problem("Unable to retrieve updated organisation.");

            return Ok(OrganisationDto.FromOrganisation(result));
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occurred while updating the organisation.");
            return Problem(e.Message);
        }
    }

    private async Task<Organisation> TransformToOrganisation(OrganisationDto organisationDto)
    {
        var mandates = await context.Mandates
            .Where(m => organisationDto.Mandates.Contains(m.Id))
            .ToListAsync();
        var users = await context.Users
            .Where(u => organisationDto.Users.Contains(u.Id))
            .ToListAsync();

        return new Organisation
        {
            Id = organisationDto.Id,
            Name = organisationDto.Name,
            Mandates = mandates,
            Users = users,
        };
    }
}
