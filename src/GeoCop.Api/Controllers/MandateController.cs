using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GeoCop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public class MandateController : ControllerBase
{
    private readonly ILogger<MandateController> logger;
    private readonly Context context;
    private readonly IValidationService validationService;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="context"></param>
    /// <param name="validationService"></param>
    public MandateController(ILogger<MandateController> logger, Context context, IValidationService validationService)
    {
        this.logger = logger;
        this.context = context;
        this.validationService = validationService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string jobId = "")
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
