using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Administration of the reference data mapping, the entries that tell ilivalidator which reference data to
/// load for a topic or scope. Routed only where the installation maintains the mapping in the admin portal,
/// see <see cref="Conventions.RefMappingConvention"/>.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = GeopilotPolicies.Admin)]
public partial class RefMappingController : ControllerBase
{
    private const int MaxNameLength = 250;

    private readonly ILogger<RefMappingController> logger;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefMappingController"/> class.
    /// </summary>
    /// <param name="logger">Logger for the instance.</param>
    /// <param name="context">Database context for the reference data mapping.</param>
    public RefMappingController(ILogger<RefMappingController> logger, Context context)
    {
        this.logger = logger;
        this.context = context;
    }

    /// <summary>
    /// Gets all entries of the reference data mapping.
    /// </summary>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns all entries of the reference data mapping.", typeof(IEnumerable<RefMapping>), "application/json")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var refMappings = await context.RefMappings
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
        return Ok(refMappings);
    }

    /// <summary>
    /// Gets the entry with the specified <paramref name="id"/>.
    /// </summary>
    [HttpGet("{id}")]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the entry with the specified id.", typeof(RefMapping), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The entry could not be found.")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var refMapping = await context.RefMappings
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        return refMapping is null ? NotFound() : Ok(refMapping);
    }

    /// <summary>
    /// Creates the <paramref name="refMapping"/> entry specified.
    /// </summary>
    /// <param name="refMapping">The entry to create.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status201Created, "The entry was created successfully.", typeof(RefMapping), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The entry could not be created due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to create an entry.")]
    public async Task<IActionResult> Create(RefMapping refMapping, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refMapping);

        if (Validate(refMapping) is { } refusal)
            return BadRequest(refusal);

        refMapping.Id = 0;
        context.RefMappings.Add(refMapping);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created reference data mapping entry <{RefMappingId}>.", refMapping.Id);

        var location = new Uri(string.Create(CultureInfo.InvariantCulture, $"/api/v1/refmapping/{refMapping.Id}"), UriKind.Relative);
        return Created(location, refMapping);
    }

    /// <summary>
    /// Updates the <paramref name="refMapping"/> entry specified.
    /// </summary>
    /// <param name="refMapping">The entry to update, identified by its id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpPut]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the updated entry.", typeof(RefMapping), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The entry could not be updated due to invalid input.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to update the entry.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The entry could not be found.")]
    public async Task<IActionResult> Edit(RefMapping refMapping, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refMapping);

        var existing = await context.RefMappings.SingleOrDefaultAsync(r => r.Id == refMapping.Id, cancellationToken);
        if (existing is null)
            return NotFound();

        if (Validate(refMapping) is { } refusal)
            return BadRequest(refusal);

        existing.Scope = refMapping.Scope;
        existing.RefDataUris = refMapping.RefDataUris;
        existing.Comment = refMapping.Comment;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated reference data mapping entry <{RefMappingId}>.", existing.Id);

        return Ok(existing);
    }

    /// <summary>
    /// Deletes the entry with the specified <paramref name="id"/>.
    /// </summary>
    [HttpDelete("{id}")]
    [SwaggerResponse(StatusCodes.Status204NoContent, "The entry was deleted.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to delete the entry.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The entry could not be found.")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await context.RefMappings.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
            return NotFound();

        logger.LogInformation("Deleted reference data mapping entry <{RefMappingId}>.", id);
        return NoContent();
    }

    /// <summary>
    /// Trims <paramref name="refMapping"/> in place and checks it against the rules of the mapping: a scope not longer
    /// than 250 characters, and at least one reference, each of the form <c>ilidata:&lt;Id&gt;</c>. ilivalidator
    /// fetches whatever the mapping names, so URLs and local paths are refused.
    /// </summary>
    /// <returns>Why the entry is refused, or <see langword="null"/> when it is valid.</returns>
    internal static string? Validate(RefMapping refMapping)
    {
        ArgumentNullException.ThrowIfNull(refMapping);

        var scope = TrimToNull(refMapping.Scope);
        refMapping.Comment = TrimToNull(refMapping.Comment);
        refMapping.RefDataUris = refMapping.RefDataUris.Select(uri => uri?.Trim() ?? string.Empty).ToList();

        if (scope is null)
            return "A scope is required.";

        if (scope.Length > MaxNameLength)
            return $"Scope must not be longer than {MaxNameLength} characters.";

        if (refMapping.RefDataUris.Count == 0)
            return "At least one reference is required.";

        if (refMapping.RefDataUris.FirstOrDefault(uri => !IlidataReference().IsMatch(uri)) is { } invalidUri)
            return $"The reference <{invalidUri}> is not of the form ilidata:<Id>.";

        refMapping.Scope = scope;
        return null;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^ilidata:[A-Za-z0-9._-]+\z")]
    private static partial Regex IlidataReference();
}
