using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for mandates.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class MandateController : ControllerBase
{
    private readonly ILogger<MandateController> logger;
    private readonly Context context;
    private readonly IValidationService validationService;
    private readonly IEnumerable<IValidator> validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting mandates.</param>
    /// <param name="validationService">The validation service providing upload file information for filetype matching.</param>
    /// <param name="validators">The validator providing information about the INTERLIS validation.</param>
    public MandateController(ILogger<MandateController> logger, Context context, IValidationService validationService, IEnumerable<IValidator> validators)
    {
        this.logger = logger;
        this.context = context;
        this.validationService = validationService;
        this.validators = validators;
    }

    /// <summary>
    /// Get a list of mandates for the current user and matching all filter criteria.
    /// </summary>
    /// <param name="jobId">Optional. Get matching mandates by validation job id.</param>
    /// <returns>List of mandates matching optional filter criteria.</returns>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of mandates associated to the current user matching the optional filter criteria.", typeof(IEnumerable<Mandate>), "application/json")]
    public async Task<IActionResult> Get(
        [FromQuery, SwaggerParameter("Filter mandates matching validation job file extension.")]
        Guid jobId = default)
    {
        logger.LogInformation("Getting mandates for job with id <{JobId}>.", jobId);

        var user = await context.GetUserByPrincipalAsync(User);
        var mandates = context.MandatesWithIncludes.AsNoTracking();

        if (!user.IsAdmin || jobId != default)
        {
            mandates = mandates.Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
        }

        if (jobId != default)
        {
            var job = validationService.GetJob(jobId);
            if (job is null)
            {
                logger.LogTrace("Validation job with id <{JobId}> was not found.", jobId);
                return Ok(Array.Empty<Mandate>());
            }

            if (string.IsNullOrEmpty(job.OriginalFileName))
            {
                logger.LogTrace("Validation job with id <{JobId}> has no associated file name.", jobId);
                return Ok(Array.Empty<Mandate>());
            }

            logger.LogTrace("Filtering mandates for job with id <{JobId}>", jobId);
            var extension = Path.GetExtension(job.OriginalFileName).ToLowerInvariant();
            mandates = mandates
                .Where(m => m.FileTypes.Contains(".*") || m.FileTypes.Select(ft => ft.ToLower()).Contains(extension));
        }

        var result = mandates.ToList();
        result.ForEach(m => m.SetCoordinateListFromPolygon());

        logger.LogInformation($"Getting mandates with for job with id <{jobId}> resulted in <{result.Count}> matching mandates.");
        return Ok(result);
    }

    /// <summary>
    /// Gets the mandate with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>The mandate.</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the mandate", typeof(Mandate), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to get individual mandates.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The mandate could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> GetById(int id)
    {
        logger.LogInformation($"Getting mandate with id <{id}>.");

        var mandate = await context.MandatesWithIncludes.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id).ConfigureAwait(false);

        if (mandate == null)
        {
            return NotFound();
        }

        mandate.SetCoordinateListFromPolygon();
        return Ok(mandate);
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="mandate"/> specified.
    /// </summary>
    /// <param name="mandate">The mandate to create.</param>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The mandate was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(Mandate mandate)
    {
        try
        {
            if (mandate == null)
                return BadRequest();

            if (!mandate.SetPolygonFromCoordinates())
                return BadRequest("Invalid coordinates for spatial extent.");

            if (!await IsValidInterlisProfile(mandate.InterlisValidationProfile))
                return BadRequest($"INTERLIS validation profile <{mandate.InterlisValidationProfile}> does not exist.");

            var organisationIds = mandate.Organisations.Select(o => o.Id).ToList();
            mandate.Organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();

            var entityEntry = await context.AddAsync(mandate).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.MandatesWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == entityEntry.Entity.Id);
            if (result == default)
                return Problem("Unable to retrieve created mandate.");

            result.SetCoordinateListFromPolygon();

            var location = new Uri(string.Format(CultureInfo.InvariantCulture, $"/api/v1/mandate/{result.Id}"), UriKind.Relative);
            return Created(location, result);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured while creating the mandate.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="mandate"/> specified.
    /// </summary>
    /// <param name="mandate">The mandate to update.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The mandate was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The mandate could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]

    public async Task<IActionResult> Edit(Mandate mandate)
    {
        try
        {
            if (mandate == null)
                return BadRequest();

            var existingMandate = await context.MandatesWithIncludes
                .FirstOrDefaultAsync(m => m.Id == mandate.Id);

            if (existingMandate == null)
                return NotFound();

            if (!mandate.SetPolygonFromCoordinates())
                return BadRequest("Invalid coordinates for spatial extent.");

            if (!await IsValidInterlisProfile(mandate.InterlisValidationProfile))
                return BadRequest($"INTERLIS validation profile <{mandate.InterlisValidationProfile}> does not exist.");

            context.Entry(existingMandate).CurrentValues.SetValues(mandate);

            var organisationIds = mandate.Organisations.Select(o => o.Id).ToList();
            var organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();
            existingMandate.Organisations.Clear();
            foreach (var organisation in organisations)
            {
                existingMandate.Organisations.Add(organisation);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.MandatesWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mandate.Id);
            if (result == default)
                return Problem("Unable to retrieve updated mandate.");

            result.SetCoordinateListFromPolygon();

            return Ok(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured while updating the mandate.");
            return Problem(e.Message);
        }
    }

    private async Task<bool> IsValidInterlisProfile(string? profile)
    {
        if (profile == null) return true;

        var interlisValidator = validators.FirstOrDefault();
        if (interlisValidator == null) return false;

        var supportedProfiles = await interlisValidator.GetSupportedProfilesAsync();
        return supportedProfiles.Any(p => string.Equals(p.Id, profile, StringComparison.Ordinal));
    }
}
