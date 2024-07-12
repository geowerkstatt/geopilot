using Geopilot.Api.Authorization;
using Geopilot.Api.DTOs;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting mandates.</param>
    /// <param name="validationService">The validation service providing upload file information for filetype matching.</param>
    public MandateController(ILogger<MandateController> logger, Context context, IValidationService validationService)
    {
        this.logger = logger;
        this.context = context;
        this.validationService = validationService;
    }

    /// <summary>
    /// Get a list of mandates for the current user and matching all filter criteria.
    /// </summary>
    /// <param name="jobId">Optional. Get matching mandates by validation job id.</param>
    /// <returns>List of mandates matching optional filter criteria.</returns>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of mandates associated to the current user matching the optional filter criteria.", typeof(IEnumerable<Mandate>), new[] { "application/json" })]
    public async Task<IActionResult> Get(
        [FromQuery, SwaggerParameter("Filter mandates matching validation job file extension.")]
        Guid jobId = default)
    {
        logger.LogInformation("Getting mandates for job with id <{JobId}>.", jobId);

        var user = await context.GetUserByPrincipalAsync(User);
        if (user == null)
            return Unauthorized();

        var mandates = context.Mandates
            .Include(m => m.Organisations)
            .Include(m => m.Deliveries)
            .AsNoTracking();

        if (!user.IsAdmin)
        {
            mandates = mandates.Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));
        }

        if (jobId != default)
        {
            var job = validationService.GetJob(jobId);
            if (job is null)
            {
                logger.LogTrace("Validation job with id <{JobId}> was not found.", jobId);
                return Ok(Array.Empty<MandateDto>());
            }

            logger.LogTrace("Filtering mandates for job with id <{JobId}>", jobId);
            var extension = Path.GetExtension(job.OriginalFileName);
            mandates = mandates
                .Where(m => m.FileTypes.Contains(".*") || m.FileTypes.Contains(extension));
        }

        var result = mandates.Select(MandateDto.FromMandate).ToList();

        logger.LogInformation($"Getting mandates with for job with id <{jobId}> resulted in <{result.Count}> matching mandates.");
        return Ok(result);
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="mandateDto"/> specified.
    /// </summary>
    /// <param name="mandateDto">The entity to create.</param>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The mandate was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]
    public async Task<IActionResult> Create(MandateDto mandateDto)
    {
        try
        {
            if (mandateDto == null)
            return BadRequest();

            var mandate = await TransformToMandate(mandateDto);

            var entityEntry = await context.AddAsync(mandate).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = entityEntry.Entity;
            var location = new Uri(string.Format(CultureInfo.InvariantCulture, $"/api/v1/mandate/{result.Id}"), UriKind.Relative);
            return Created(location, MandateDto.FromMandate(result));
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured while creating the mandate.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="mandateDto"/> specified.
    /// </summary>
    /// <param name="mandateDto">The mandate to create.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The mandate was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The mandate could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]

    public async Task<IActionResult> Edit(MandateDto mandateDto)
    {
        try
        {
            if (mandateDto == null)
                return BadRequest();

            var updatedMandate = await TransformToMandate(mandateDto);
            var existingMandate = await context.Mandates
                .Include(m => m.Organisations)
                .Include(m => m.Deliveries)
                .FirstOrDefaultAsync(m => m.Id == mandateDto.Id);

            if (existingMandate == null)
                return NotFound();

            context.Entry(existingMandate).CurrentValues.SetValues(updatedMandate);

            existingMandate.Organisations.Clear();
            foreach (var organisation in updatedMandate.Organisations)
            {
                if (!existingMandate.Organisations.Contains(organisation))
                    existingMandate.Organisations.Add(organisation);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
            return Ok(MandateDto.FromMandate(updatedMandate));
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured while creating the mandate.");
            return Problem(e.Message);
        }
    }

    private async Task<Mandate> TransformToMandate(MandateDto mandateDto)
    {
        var organisations = await context.Organisations.Where(o => mandateDto.Organisations.Contains(o.Id)).ToListAsync();
        var deliveries = await context.Deliveries.Where(d => mandateDto.Deliveries.Contains(d.Id)).ToListAsync();
        return new Mandate
        {
            Id = mandateDto.Id,
            Name = mandateDto.Name,
            FileTypes = mandateDto.FileTypes.ToArray(),
            SpatialExtent = Geometry.DefaultFactory.CreatePolygon(),
            Organisations = organisations,
            Deliveries = deliveries,
        };
    }
}
