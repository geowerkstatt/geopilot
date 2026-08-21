using Geopilot.Api.Enums;

namespace Geopilot.Api.Services;

/// <summary>
/// Registers everything the upload feature needs: the shared upload policy, the storage backend the
/// configuration selects, and the backend-independent orchestration, preflight, scan and cleanup services.
/// </summary>
public static class UploadServiceExtensions
{
    /// <summary>
    /// Registers the upload services on <paramref name="builder"/>.
    /// Only the storage backend differs between the modes; everything downstream (orchestration,
    /// preflight, scan, cleanup) works against <see cref="IUploadStorage"/>. Each backend's options are
    /// bound and validated only in its own branch, so a deployment configures exactly one of the two sections.
    /// </summary>
    /// <returns>The configured <see cref="UploadBackend"/>, which also decides the endpoint mapping.</returns>
    public static UploadBackend AddUploadServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Configuration.EnsureNoLegacyCloudStorageSection();
        builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));

        var uploadBackend = builder.Configuration.GetValue($"{UploadOptions.SectionName}:{nameof(UploadOptions.Backend)}", UploadBackend.Cloud);
        if (uploadBackend == UploadBackend.Direct)
        {
            builder.Services.AddOptions<UploadDirectOptions>()
                .BindConfiguration(UploadDirectOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton<DirectUploadStorage>();
            builder.Services.AddSingleton<IUploadStorage>(sp => sp.GetRequiredService<DirectUploadStorage>());
        }
        else
        {
            builder.Services.AddOptions<UploadCloudOptions>()
                .BindConfiguration(UploadCloudOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddSingleton<IUploadStorage, AzureBlobUploadStorage>();
        }

        builder.Services.AddTransient<IUploadOrchestrationService, UploadOrchestrationService>();
        builder.Services.AddHostedService<UploadCleanupService>();
        builder.Services.AddPreflightChannel();
        builder.Services.AddHostedService<PreflightBackgroundService>();

        if (builder.Configuration.GetValue<bool>("ClamAV:Enabled"))
        {
            builder.Services.AddTransient<IUploadScanService, ClamAvScanService>();
        }
        else
        {
            builder.Services.AddTransient<IUploadScanService, NoOpScanService>();
        }

        return uploadBackend;
    }
}
