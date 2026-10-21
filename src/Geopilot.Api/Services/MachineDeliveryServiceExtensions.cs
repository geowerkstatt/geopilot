namespace Geopilot.Api.Services;

/// <summary>
/// Registers what machine based delivery needs. Its attempts are processing jobs with a delivery intent, which the
/// job lifecycle declares once their run allows it, so the options are all that is left to register.
/// </summary>
public static class MachineDeliveryServiceExtensions
{
    /// <summary>
    /// Registers the machine delivery options on <paramref name="builder"/>. The capability is read here, in the
    /// builder phase, because it also decides whether the resources are routed at all.
    /// </summary>
    /// <returns>Whether this installation offers machine based delivery.</returns>
    public static bool AddMachineDelivery(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<MachineDeliveryOptions>(builder.Configuration.GetSection(MachineDeliveryOptions.SectionName));
        return builder.Configuration.GetValue($"{MachineDeliveryOptions.SectionName}:{nameof(MachineDeliveryOptions.Enabled)}", false);
    }
}
