using Geopilot.Api.Enums;
using Microsoft.Extensions.Options;

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
    /// preflight, scan, cleanup) works against <see cref="IUploadStorage"/>. Each backend's options, and in
    /// the cloud mode each provider's options, are bound and validated only in their own branch, so a
    /// deployment configures only the sections of its backend and provider.
    /// </summary>
    /// <returns>The configured <see cref="UploadBackend"/>, which also decides the endpoint mapping.</returns>
    /// <exception cref="InvalidOperationException">Upload:Backend or Upload:Cloud:Provider is not a supported value.</exception>
    public static UploadBackend AddUploadServices(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Configuration.EnsureNoLegacyCloudStorageSection();
        builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));

        var uploadBackend = builder.Configuration.GetValue($"{UploadOptions.SectionName}:{nameof(UploadOptions.Backend)}", UploadBackend.Cloud);
        switch (uploadBackend)
        {
            case UploadBackend.Cloud:
                AddCloudUploadStorage(builder);
                break;

            case UploadBackend.Direct:
                AddDirectUploadStorage(builder);
                break;

            default:
                throw new InvalidOperationException($"Unsupported Upload:Backend '{uploadBackend}'.");
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

    private static void AddDirectUploadStorage(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IValidateOptions<UploadDirectOptions>, UploadDirectDirectoryValidator>();
        builder.Services.AddOptions<UploadDirectOptions>()
            .BindConfiguration(UploadDirectOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<DirectUploadStorage>();
        builder.Services.AddSingleton<IUploadStorage>(sp => sp.GetRequiredService<DirectUploadStorage>());
        builder.Services.AddHealthChecks()
            .AddCheck<UploadDirectoryHealthCheck>("UploadDirectory", tags: [HealthEndpoints.ReadyTag]);
    }

    private static void AddCloudUploadStorage(WebApplicationBuilder builder)
    {
        var cloudOptions = builder.Services.AddOptions<UploadCloudOptions>()
            .BindConfiguration(UploadCloudOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = builder.Configuration.GetValue($"{UploadCloudOptions.SectionName}:{nameof(UploadCloudOptions.Provider)}", CloudStorageProvider.AzureBlob);
        switch (provider)
        {
            case CloudStorageProvider.AzureBlob:
                cloudOptions.Validate(
                    options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                    "Upload:Cloud:ConnectionString is required for the AzureBlob provider.");
                builder.Services.AddSingleton<IUploadStorage, AzureBlobUploadStorage>();
                break;

            case CloudStorageProvider.S3:
                builder.Services.AddOptions<UploadS3Options>()
                    .BindConfiguration(UploadS3Options.SectionName)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();
                builder.Services.AddSingleton<S3UploadStorage>();
                builder.Services.AddSingleton<IUploadStorage>(sp => sp.GetRequiredService<S3UploadStorage>());
                builder.Services.AddHostedService(sp => sp.GetRequiredService<S3UploadStorage>());
                break;

            default:
                throw new InvalidOperationException($"Unsupported Upload:Cloud:Provider '{provider}'.");
        }
    }
}
