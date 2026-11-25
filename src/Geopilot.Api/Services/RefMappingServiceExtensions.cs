namespace Geopilot.Api.Services;

/// <summary>
/// Registers the reference data mapping options.
/// </summary>
public static class RefMappingServiceExtensions
{
    /// <summary>
    /// Registers the reference data mapping options on <paramref name="builder"/>. The switch is read here, in the
    /// builder phase, because it also decides whether the resource is routed at all.
    /// </summary>
    /// <returns>Whether this installation maintains the reference data mapping in the admin portal.</returns>
    public static bool AddRefMapping(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<RefMappingOptions>(builder.Configuration.GetSection(RefMappingOptions.SectionName));
        return builder.Configuration.GetValue($"{RefMappingOptions.SectionName}:{nameof(RefMappingOptions.Enabled)}", false);
    }
}
