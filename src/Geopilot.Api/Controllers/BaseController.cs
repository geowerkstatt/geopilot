using Geopilot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Base controller managing geopilot entities.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class BaseController<TEntity> : ControllerBase
    where TEntity : IIdentifiable
{
    private readonly Context context;
    private readonly ILogger<TEntity> logger;
    private readonly string entityTypeName;

    /// <summary>
    /// Gets the <see cref="Context"/> used by the controller.
    /// </summary>
    protected Context Context => context;

    /// <summary>
    /// Gets the <see cref="ILogger{TEntity}"/> used by the controller.
    /// </summary>
    protected ILogger<TEntity> Logger => logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseController{TEntity}"/> class.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="logger"></param>
    protected BaseController(Context context, ILogger<TEntity> logger)
    {
        this.context = context;
        this.logger = logger;
        this.entityTypeName = typeof(TEntity).Name.ToLower(System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="entity"/> specified.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    [HttpPost]
    public virtual async Task<IActionResult> Create(TEntity entity)
    {
        logger.LogInformation($"Creating new {entityTypeName}.");
        if (entity == null) return BadRequest();

        try
        {
            var entityEntry = await context.AddAsync(entity).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var result = (TEntity?)entityEntry.Entity;
            var location = new Uri(string.Format(CultureInfo.InvariantCulture, $"/api/v1/{entityTypeName}/{result.Id}"), UriKind.Relative);
            return Created(location, result);
        }
        catch (Exception e)
        {
            var message = $"An error occured while creating the {entityTypeName}.";
            logger.LogError(e, message);
            return Problem(message);
        }
    }

    /// <summary>
    /// Asynchronously creates the <paramref name="entity"/> specified.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    [HttpPut]
    public virtual async Task<IActionResult> Edit(TEntity entity)
    {
        logger.LogInformation($"Update {entityTypeName}.");
        if (entity == null) return BadRequest();

        try
        {
            var entityToEdit = (TEntity?)await context.FindAsync(typeof(TEntity), entity.Id).ConfigureAwait(false);

            if (entityToEdit == null)
            {
                return NotFound();
            }

            context.Entry(entityToEdit).CurrentValues.SetValues(entity);
            await context.SaveChangesAsync().ConfigureAwait(false);
            return Ok(entity);
        }
        catch (Exception e)
        {
            var message = $"An error occured while creating the {entityTypeName}.";
            logger.LogError(e, message);
            return Problem(message);
        }
    }
}
