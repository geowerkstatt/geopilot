using Geopilot.Api.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Geopilot.Api.Conventions;

/// <summary>
/// Keeps the administration of the reference data mapping out of the application while the installation does
/// not maintain the mapping in the admin portal (<c>RefMapping:Enabled</c>). Removing the controller takes its
/// routes and OpenAPI operations with it, as <see cref="MachineDeliveryConvention"/> does.
/// </summary>
public class RefMappingConvention : IApplicationModelConvention
{
    private readonly bool refMappingEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefMappingConvention"/> class.
    /// </summary>
    /// <param name="refMappingEnabled">Whether this installation maintains the reference data mapping in the admin portal.</param>
    public RefMappingConvention(bool refMappingEnabled)
    {
        this.refMappingEnabled = refMappingEnabled;
    }

    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (refMappingEnabled)
            return;

        application.Controllers.Remove(application.Controllers.Single(controller => controller.ControllerType.AsType() == typeof(RefMappingController)));
    }
}
