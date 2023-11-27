using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace GeoCop.Api.Controllers;

/// <summary>
/// Controller for listing mandates.
/// </summary>
[ApiController]
[Authorize]
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
    /// <param name="context">Database Context for getting mandates.</param>
    /// <param name="validationService">The validation service providing upload file infomration for filetype matchin.</param>
    public MandateController(ILogger<MandateController> logger, Context context, IValidationService validationService)
    {
        this.logger = logger;
        this.context = context;
        this.validationService = validationService;
    }

    /// <summary>
    /// Get a list of mandates for the current user & matchin all filter criteria.
    /// </summary>
    /// <param name="jobId">If given the mandates are filtered for matching mandate.</param>
    /// <returns>List of mandates matching filter criteria.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns list of mandates associated the user matching filter criteria.", typeof(IEnumerable<DeliveryMandate>), new[] { "application/json" })]
    public async Task<IActionResult> Get(
        [FromQuery, SwaggerParameter("Filter mandates matching validation job file extension.")]
        string jobId = "")
    {
        var user = await context.GetUserByPrincipalAsync(User);

        var mandates = context.DeliveryMandates
                .Where(m => m.Organisations
                .SelectMany(o => o.Users)
                .Contains(user));

        if (!string.IsNullOrEmpty(jobId) && Guid.TryParse(jobId, out var guid))
        {
            var job = validationService.GetJob(guid);
            if (job is null)
                return Ok(Array.Empty<DeliveryMandate>());

            var extension = Path.GetExtension(job.OriginalFileName);
            mandates = mandates
                .Where(m => m.FileTypes.Contains(".*") || m.FileTypes.Contains(extension));
        }

        var result = await mandates.ToListAsync();
        return Ok(mandates);
    }
}
