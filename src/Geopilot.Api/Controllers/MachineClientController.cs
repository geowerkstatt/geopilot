using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Administration of the machine clients that deliver through the API. Registration is explicit: an
/// administrator enters the identifier a client's token carries and the organisations it delivers for.
/// Nothing creates a client from a token, because every token with the right audience would otherwise be
/// a registered client. Routed only where the installation offers machine delivery, see
/// <see cref="Conventions.MachineDeliveryConvention"/>.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class MachineClientController : ControllerBase
{
    private readonly ILogger<MachineClientController> logger;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineClientController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for getting machine clients.</param>
    public MachineClientController(ILogger<MachineClientController> logger, Context context)
    {
        this.logger = logger;
        this.context = context;
    }

    /// <summary>
    /// Gets a list of machine clients.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of machine clients.", typeof(IEnumerable<MachineClient>), "application/json")]
    public List<MachineClient> Get()
    {
        logger.LogInformation("Getting machine clients.");

        return context.MachineClientsWithIncludes
            .AsNoTracking()
            .ToList();
    }

    /// <summary>
    /// Gets the machine client with the specified <paramref name="id"/>.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the machine client with the specified id.", typeof(MachineClient), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The machine client could not be found.")]
    public async Task<IActionResult> GetById(int id)
    {
        var machineClient = await context.MachineClientsWithIncludes
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id);

        if (machineClient == default)
            return NotFound();

        return Ok(machineClient);
    }

    /// <summary>
    /// Asynchronously registers the <paramref name="machineClient"/> specified.
    /// </summary>
    /// <param name="machineClient">The machine client to register.</param>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status201Created, "The machine client was registered successfully.", typeof(MachineClient), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The machine client could not be registered due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to register a machine client.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The identifier is already registered, or it belongs to a user.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(MachineClient machineClient)
    {
        try
        {
            if (machineClient == null)
                return BadRequest();

            if (Validate(machineClient) is { } invalid)
                return invalid;

            if (await IsUserIdentifierAsync(machineClient.AuthIdentifier))
                return UserIdentifierConflict(machineClient);

            var organisationIds = machineClient.Organisations.Select(o => o.Id).ToList();
            machineClient.Organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();

            var entityEntry = await context.AddAsync(machineClient).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.MachineClientsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == entityEntry.Entity.Id);
            if (result == default)
                return Problem("Unable to retrieve created machine client.");

            var location = new Uri(string.Create(CultureInfo.InvariantCulture, $"/api/v1/machineclient/{result.Id}"), UriKind.Relative);
            return Created(location, result);
        }
        catch (DbUpdateException e) when (IsIdentifierConflict(e))
        {
            logger.LogInformation("Rejected machine client registration because the identifier is already registered.");
            return Conflict($"Machine client identifier <{machineClient?.AuthIdentifier}> is already registered.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while registering the machine client.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Asynchronously updates the <paramref name="machineClient"/> specified.
    /// </summary>
    /// <param name="machineClient">The machine client to update.</param>
    [HttpPut]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the updated machine client.", typeof(MachineClient), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The machine client could not be found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The machine client could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to update the machine client.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The identifier is already registered, or it belongs to a user.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The machine client could not be updated due to an internal server error.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Edit(MachineClient machineClient)
    {
        try
        {
            if (machineClient == null)
                return BadRequest();

            var existingClient = await context.MachineClientsWithIncludes.SingleOrDefaultAsync(c => c.Id == machineClient.Id);
            if (existingClient == null)
                return NotFound();

            if (Validate(machineClient) is { } invalid)
                return invalid;

            if (await IsUserIdentifierAsync(machineClient.AuthIdentifier))
                return UserIdentifierConflict(machineClient);

            existingClient.AuthIdentifier = machineClient.AuthIdentifier;
            existingClient.Name = machineClient.Name;
            existingClient.State = machineClient.State;

            var organisationIds = machineClient.Organisations.Select(o => o.Id).ToList();
            var organisations = await context.Organisations
                .Where(o => organisationIds.Contains(o.Id))
                .ToListAsync();
            existingClient.Organisations.Clear();
            existingClient.Organisations.AddRange(organisations);

            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = await context.MachineClientsWithIncludes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == machineClient.Id);

            return Ok(result);
        }
        catch (DbUpdateException e) when (IsIdentifierConflict(e))
        {
            logger.LogInformation("Rejected update of machine client <{MachineClientId}> because the identifier is already registered.", machineClient?.Id);
            return Conflict($"Machine client identifier <{machineClient?.AuthIdentifier}> is already registered.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating the machine client.");
            return Problem(e.Message);
        }
    }

    /// <summary>
    /// Trims the identifier and the name and refuses a client that lacks either. The identifier is pasted
    /// from the identity provider or from a log line, and a stray space would register a client no token
    /// can ever match.
    /// </summary>
    /// <returns>The refusal, or <see langword="null"/> when the client is valid.</returns>
    private BadRequestObjectResult? Validate(MachineClient machineClient)
    {
        if (string.IsNullOrWhiteSpace(machineClient.AuthIdentifier))
            return BadRequest("The identifier of the machine client is required.");

        if (string.IsNullOrWhiteSpace(machineClient.Name))
            return BadRequest("The name of the machine client is required.");

        machineClient.AuthIdentifier = machineClient.AuthIdentifier.Trim();
        machineClient.Name = machineClient.Name.Trim();
        return null;
    }

    /// <summary>
    /// A subject registered as a machine client is never a user again (see <see cref="GeopilotUserResolver"/>),
    /// so registering the subject of an existing user would lock that person out. Refused up front, because
    /// no index can express it.
    /// </summary>
    private Task<bool> IsUserIdentifierAsync(string authIdentifier)
        => context.Users.AnyAsync(u => u.AuthIdentifier == authIdentifier);

    private ConflictObjectResult UserIdentifierConflict(MachineClient machineClient)
    {
        logger.LogInformation("Rejected machine client <{MachineClientId}> because the identifier belongs to a user.", machineClient.Id);
        return Conflict($"Identifier <{machineClient.AuthIdentifier}> belongs to a user and cannot be registered as a machine client.");
    }

    /// <summary>
    /// Whether the save failed because the identifier is already registered. The unique index is the only
    /// place this is decided: a check before saving would still race a concurrent save.
    /// </summary>
    private static bool IsIdentifierConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            && string.Equals(postgresException.ConstraintName, Context.MachineClientIdentifierIndexName, StringComparison.Ordinal);
}
