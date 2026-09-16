using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
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
    private readonly IMandateService mandateService;
    private readonly IPipelineService pipelineService;
    private readonly MachineDeliveryOptions machineDeliveryOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MandateController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting mandates.</param>
    /// <param name="mandateService">The mandate service providing mandate filtering and retrieval.</param>
    /// <param name="pipelineService">The pipeline service providing information about available pipelines for validation during creating or updating mandates.</param>
    /// <param name="machineDeliveryOptions">The machine delivery settings, which decide whether a mandate carries a key.</param>
    public MandateController(
        ILogger<MandateController> logger,
        Context context,
        IMandateService mandateService,
        IPipelineService pipelineService,
        IOptions<MachineDeliveryOptions> machineDeliveryOptions)
    {
        ArgumentNullException.ThrowIfNull(machineDeliveryOptions);

        this.logger = logger;
        this.context = context;
        this.mandateService = mandateService;
        this.pipelineService = pipelineService;
        this.machineDeliveryOptions = machineDeliveryOptions.Value;
    }

    /// <summary>
    /// Gets a list of all mandates that the current user has access to and match all filter criteria.
    /// </summary>
    /// <param name="uploadId">Only mandates that accept the uploaded files' extensions are returned.</param>
    /// <returns>List of mandates matching the filter criteria.</returns>
    [HttpGet("summary")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Gets a list of all mandates that the current user has access to and match all filter criteria.", typeof(IEnumerable<MandateSummary>), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The request is missing an uploadId.")]
    public async Task<IActionResult> GetSummary(
        [FromQuery, SwaggerParameter("Filter mandates matching the uploaded files' extensions.")]
        Guid uploadId)
    {
        logger.LogInformation("Getting list of mandate summaries for upload with id <{UploadId}>.", uploadId);

        if (uploadId == default)
        {
            return BadRequest("Upload id is required.");
        }

        var user = User?.Identity?.IsAuthenticated == true
            ? await context.GetUserByPrincipalAsync(User)
            : null;

        var result = await mandateService.GetMandateSummariesAsync(user, uploadId);
        logger.LogInformation("Getting list of mandate summaries for upload with id <{UploadId}> resulted in <{ResultCount}> matching mandates.", uploadId, result.Count);
        return Ok(result);
    }

    /// <summary>
    /// Gets a list of all mandate keys for automated deliveries that are in use.
    /// </summary>
    /// <returns>List of all mandate keys, or an empty list while machine delivery is not enabled.</returns>
    [HttpGet("keys")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Gets a list of all mandate keys.", typeof(IEnumerable<string>), "application/json")]
    public async Task<IActionResult> GetKeys()
    {
        if (!machineDeliveryOptions.Enabled)
        {
            logger.LogInformation("Reporting no mandate keys because machine delivery is not enabled.");
            return Ok(Array.Empty<string>());
        }

        var result = await mandateService.GetMandateKeysAsync();
        logger.LogInformation("Getting list of mandate keys resulted in <{ResultCount}> unique keys.", result.Count);
        return Ok(result);
    }

    /// <summary>
    /// Gets a list of all mandates.
    /// </summary>
    /// <returns>List of mandates.</returns>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Gets a list of all mandates.", typeof(IEnumerable<Mandate>), "application/json")]
    public async Task<IActionResult> Get()
    {
        var result = await mandateService.GetMandatesAsync();
        result.ForEach(m => m.SetCoordinateListFromPolygon());

        logger.LogInformation("Getting all mandates resulted in <{ResultCount}> matching mandates.", result.Count);
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
    [SwaggerResponse(StatusCodes.Status409Conflict, "The mandate key is already in use by another mandate.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. ", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(Mandate mandate)
    {
        try
        {
            if (mandate == null)
                return BadRequest();

            if (!mandate.SetPolygonFromCoordinates())
                return BadRequest("Invalid coordinates for spatial extent.");

            if (!IsValidPipeline(mandate.PipelineId))
                return BadRequest($"Pipeline <{mandate.PipelineId}> does not exist.");

            var organisationIds = mandate.Organisations.Select(o => o.Id).ToList();
            mandate.Organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();

            ApplyKey(mandate, storedKey: null);

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
        catch (DbUpdateException e) when (IsKeyConflict(e))
        {
            logger.LogInformation("Rejected mandate creation because the key is already in use.");
            return Problem($"Mandate key <{mandate?.Key}> is already in use.", statusCode: StatusCodes.Status409Conflict);
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
    [SwaggerResponse(StatusCodes.Status409Conflict, "The mandate key is already in use by another mandate.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request.", typeof(ProblemDetails), "application/json")]
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

            if (!IsValidPipeline(mandate.PipelineId))
                return BadRequest($"Pipeline <{mandate.PipelineId}> does not exist.");

            ApplyKey(mandate, existingMandate.Key);

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
        catch (DbUpdateException e) when (IsKeyConflict(e))
        {
            logger.LogInformation("Rejected update of mandate <{MandateId}> because the key is already in use.", mandate?.Id);
            return Problem($"Mandate key <{mandate?.Key}> is already in use.", statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured while updating the mandate.");
            return Problem(e.Message);
        }
    }

    private bool IsValidPipeline(string? pipelineId)
    {
        if (string.IsNullOrEmpty(pipelineId)) return false;

        var pipeline = pipelineService.GetById(pipelineId);
        return pipeline != null;
    }

    /// <summary>
    /// Decides which key the mandate is saved with. While machine delivery is off the administration does
    /// not render the field, so its payload carries no key at all: taking that literally would erase the
    /// key of every mandate on its next save, which is why the stored one is kept. An incoming key is then
    /// ignored rather than rejected, so that switching the capability off does not start failing saves on
    /// mandates that are otherwise valid.
    /// </summary>
    /// <param name="mandate">The mandate about to be saved.</param>
    /// <param name="storedKey">The key the mandate carries in the database, or <c>null</c> when it is new.</param>
    private void ApplyKey(Mandate mandate, string? storedKey)
    {
        if (machineDeliveryOptions.Enabled)
        {
            NormalizeKey(mandate);
            return;
        }

        if (!string.IsNullOrWhiteSpace(mandate.Key))
            logger.LogInformation("Ignored the key sent for a mandate because machine delivery is not enabled.");

        mandate.Key = storedKey;
    }

    /// <summary>
    /// Trims the key and turns a blank one into null. Any number of mandates may carry no key, but only
    /// as null: the unique index would reject the second empty string. Trimming keeps a pasted key with
    /// stray spaces from becoming a key that no machine client can ever match.
    /// </summary>
    private static void NormalizeKey(Mandate mandate)
        => mandate.Key = string.IsNullOrWhiteSpace(mandate.Key) ? null : mandate.Key.Trim();

    /// <summary>
    /// Whether the save failed because the mandate key is already taken. The unique index is the only
    /// place this is decided: a check before saving would still race a concurrent save.
    /// </summary>
    private static bool IsKeyConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            && string.Equals(postgresException.ConstraintName, Context.MandateKeyIndexName, StringComparison.Ordinal);
}
