using Geopilot.Api.Processing;

namespace Geopilot.Api.Services;

/// <summary>
/// Registers what machine based delivery needs: the bookkeeping of the attempts, and, only where the
/// installation offers the capability, the handler that declares their deliveries once a run finishes.
/// </summary>
public static class MachineDeliveryServiceExtensions
{
    /// <summary>
    /// Registers the machine delivery services on <paramref name="builder"/>. The capability is read here, in
    /// the builder phase, because it also decides whether the resources are routed at all.
    /// </summary>
    /// <returns>Whether this installation offers machine based delivery.</returns>
    public static bool AddMachineDelivery(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<MachineDeliveryOptions>(builder.Configuration.GetSection(MachineDeliveryOptions.SectionName));
        builder.Services.AddSingleton<ISubmissionStore, SubmissionStore>();

        var enabled = builder.Configuration.GetValue($"{MachineDeliveryOptions.SectionName}:{nameof(MachineDeliveryOptions.Enabled)}", false);
        if (enabled)
        {
            builder.Services.AddTransient<IJobCompletionHandler, MachineDeliveryCompletionHandler>();
        }

        return enabled;
    }
}
