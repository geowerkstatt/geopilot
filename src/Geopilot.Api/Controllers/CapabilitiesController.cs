using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for the capabilities this installation offers. Read only: capabilities are configured
/// on the server and reported here, never changed through the API.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = GeopilotPolicies.Admin)]
public class CapabilitiesController : ControllerBase
{
    private readonly MachineDeliveryOptions machineDeliveryOptions;
    private readonly DeliveryOptions deliveryOptions;
    private readonly RefMappingOptions refMappingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilitiesController"/> class.
    /// </summary>
    /// <param name="machineDeliveryOptions">The machine delivery settings.</param>
    /// <param name="deliveryOptions">The delivery settings, with the delete window of the uploaders.</param>
    /// <param name="refMappingOptions">The reference data mapping settings.</param>
    public CapabilitiesController(IOptions<MachineDeliveryOptions> machineDeliveryOptions, IOptions<DeliveryOptions> deliveryOptions, IOptions<RefMappingOptions> refMappingOptions)
    {
        ArgumentNullException.ThrowIfNull(machineDeliveryOptions);
        ArgumentNullException.ThrowIfNull(deliveryOptions);
        ArgumentNullException.ThrowIfNull(refMappingOptions);

        this.machineDeliveryOptions = machineDeliveryOptions.Value;
        this.deliveryOptions = deliveryOptions.Value;
        this.refMappingOptions = refMappingOptions.Value;
    }

    /// <summary>
    /// Gets the capabilities of this installation.
    /// </summary>
    /// <returns>The configured capabilities.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The capabilities of this installation.", typeof(CapabilitiesResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The current user is not authorized to read the capabilities.")]
    public CapabilitiesResponse Get()
        => new(machineDeliveryOptions.Enabled, deliveryOptions.DeleteWindowCloses, refMappingOptions.Enabled);
}
