using Geopilot.Api.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Keeps the machine delivery resources out of the application while the installation does not offer the
/// capability. Removing the controller from the model rather than refusing its requests takes the routes and
/// the OpenAPI operations with it in one step, so a capability nobody bought is not visible at all.
/// </summary>
public class MachineDeliveryConvention : IApplicationModelConvention
{
    private readonly bool machineDeliveryEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineDeliveryConvention"/> class.
    /// </summary>
    /// <param name="machineDeliveryEnabled">Whether this installation offers machine based delivery.</param>
    public MachineDeliveryConvention(bool machineDeliveryEnabled)
    {
        this.machineDeliveryEnabled = machineDeliveryEnabled;
    }

    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (machineDeliveryEnabled)
            return;

        var machineDeliveryControllers = application.Controllers
            .Where(controller => controller.ControllerType.AsType() == typeof(SubmissionController))
            .ToList();

        foreach (var controller in machineDeliveryControllers)
        {
            application.Controllers.Remove(controller);
        }
    }
}
