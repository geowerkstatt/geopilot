using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for mandates.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class MandateController : BaseController<Mandate>
{
    private readonly IValidationService validationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting mandates.</param>
    /// <param name="validationService">The validation service providing upload file information for filetype matching.</param>
    public MandateController(ILogger<Mandate> logger, Context context, IValidationService validationService)
        : base(context, logger)
    {
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
        Logger.LogInformation("Getting mandates for job with id <{JobId}>.", jobId);

        var user = await Context.GetUserByPrincipalAsync(User);
        if (user == null)
            return Unauthorized();

        var mandates = Context.Mandates
            .Where(m => m.Organisations.SelectMany(o => o.Users).Any(u => u.Id == user.Id));

        if (jobId != default)
        {
            var job = validationService.GetJob(jobId);
            if (job is null)
            {
                Logger.LogTrace("Validation job with id <{JobId}> was not found.", jobId);
                return Ok(Array.Empty<Mandate>());
            }

            Logger.LogTrace("Filtering mandates for job with id <{JobId}>", jobId);
            var extension = Path.GetExtension(job.OriginalFileName);
            mandates = mandates
                .Where(m => m.FileTypes.Contains(".*") || m.FileTypes.Contains(extension));
        }

        var result = await mandates.ToListAsync();

        Logger.LogInformation("Getting mandates with for job with id <{JobId}> resulted in <{MatchingMandatesCount}> matching mandates.", jobId, result.Count);
        return Ok(mandates);
    }

    /// <inheritdoc/>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The mandate was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]
    public override async Task<IActionResult> Create(Mandate entity)
    {
        var user = await Context.GetUserByPrincipalAsync(User);
        if (user == null || !user.IsAdmin)
            return Unauthorized();

        return await base.Create(entity);
    }

    /// <inheritdoc/>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The mandate was updated successfully.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The mandate could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The mandate could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to edit a mandate.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), new[] { "application/json" })]

    public override async Task<IActionResult> Edit(Mandate entity)
    {
        var user = await Context.GetUserByPrincipalAsync(User);
        if (user == null || !user.IsAdmin)
            return Unauthorized();

        return await base.Edit(entity);
    }
}
