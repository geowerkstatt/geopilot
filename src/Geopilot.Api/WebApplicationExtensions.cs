using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Visualization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Geopilot.Api;

/// <summary>
/// Provides extension methods for configuring the WebApplication during startup.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Validates the pipeline configuration on startup and throws if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when pipeline configuration is invalid.</exception>
    public static void ValidatePipelineConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));

        var validationResult = app.Services.GetRequiredService<IPipelineFactory>().ValidateDefinition();
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(validationResult.ErrorMessage);
        }
    }

    /// <summary>
    /// Logs the time zone the uploader's delete window closes in, when <see cref="DeliveryOptions.DeleteRestrictInterval"/>
    /// depends on it: a daily "0 0 * * *" means midnight on the server's clock.
    /// </summary>
    public static void LogDeleteWindowTimeZone(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));

        var options = app.Services.GetRequiredService<IOptions<DeliveryOptions>>().Value;
        if (options.UploaderDeleteEnabled && options.DeleteRestrictIntervalExpression is not null)
        {
            app.Logger.LogInformation(
                "Uploaders can delete a delivery until the next '{DeleteRestrictInterval}' in time zone {TimeZone}.",
                options.DeleteRestrictInterval,
                options.TimeZone.Id);
        }
    }

    /// <summary>
    /// Checks the reaction set every mandate chose, once the database is migrated. A set the definition no
    /// longer has and a delete window that never closes stop the start, because both are fixed in the configuration.
    /// Values a channel refuses only warn, because a mandate can only be corrected while the application runs.
    /// </summary>
    public static void ValidateMandateReactionSets(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));

        using var scope = app.Services.CreateScope();
        var errors = CheckMandateReactionSets(
            scope.ServiceProvider.GetRequiredService<Context>(),
            scope.ServiceProvider.GetRequiredService<IReactionSetValidator>(),
            app.Logger);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>
    /// Returns what stops the start in the reaction sets of the mandates, and logs the values the channels
    /// refuse as warnings.
    /// </summary>
    internal static IReadOnlyList<string> CheckMandateReactionSets(Context context, IReactionSetValidator validator, ILogger logger)
    {
        var mandates = context.Mandates
            .AsNoTracking()
            .Where(mandate => mandate.ReactionSetId != null)
            .Select(mandate => new { mandate.Id, mandate.ReactionSetId, mandate.ReactionSetValues })
            .ToList();

        var errors = new List<string>();
        foreach (var mandate in mandates)
        {
            var check = validator.Check(mandate.ReactionSetId, mandate.ReactionSetValues ?? []);
            if (check.SetError is not null)
                errors.Add($"Mandate {mandate.Id}: {check.SetError}");

            foreach (var valueError in check.ValueErrors)
            {
                logger.LogWarning(
                    "Mandate {MandateId} supplies values its reaction set {ReactionSetId} refuses: {Error}",
                    mandate.Id,
                    mandate.ReactionSetId,
                    valueError);
            }
        }

        return errors;
    }

    /// <summary>
    /// Serves the frontend files from the web root. Files in the directory configured as <c>PublicAssetsOverride</c>
    /// take precedence, so an operator can replace single files (imprint, logo, ...) by mounting a directory, without
    /// writing into the web root. index.html is never served as a static file, <see cref="MapSpaFallback"/> serves it
    /// with the CSP nonce. Must run before <see cref="MapSpaFallback"/>.
    /// </summary>
    public static void UseFrontendStaticFiles(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var overrideDirectory = configuration["PublicAssetsOverride"];
        if (!string.IsNullOrEmpty(overrideDirectory))
        {
            if (Directory.Exists(overrideDirectory))
            {
                app.Environment.WebRootFileProvider = new CompositeFileProvider(
                    new PhysicalFileProvider(Path.GetFullPath(overrideDirectory)),
                    app.Environment.WebRootFileProvider);
                app.Logger.LogInformation("Serving public assets from {PublicAssetsOverride} in front of the web root.", overrideDirectory);
            }
            else
            {
                app.Logger.LogWarning("PublicAssetsOverride directory {PublicAssetsOverride} does not exist, serving the web root only.", overrideDirectory);
            }
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                if (string.Equals(ctx.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.StatusCode = StatusCodes.Status404NotFound;
                    ctx.Context.Response.ContentLength = 0;
                    ctx.Context.Response.Body = Stream.Null;
                }
            },
        });
    }

    /// <summary>
    /// Maps the SPA fallback route with CSP headers and nonce-based script/style injection.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        // Read through the file provider so an index.html in PublicAssetsOverride takes precedence.
        var indexHtmlFile = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!indexHtmlFile.Exists)
        {
            return;
        }

        using var indexHtmlReader = new StreamReader(indexHtmlFile.CreateReadStream());
        var indexHtmlTemplate = indexHtmlReader.ReadToEnd();
        var authorityOrigin = new Uri(configuration["Auth:Authority"]!).GetLeftPart(UriPartial.Authority);
        var blobEndpoint = configuration["Upload:Cloud:BlobEndpoint"];
        if (!string.IsNullOrWhiteSpace(blobEndpoint))
        {
            if (!Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var blobUri))
                throw new InvalidOperationException($"Upload:Cloud:BlobEndpoint '{blobEndpoint}' is not a valid absolute URI.");
            blobEndpoint = blobUri.GetLeftPart(UriPartial.Authority);
        }

        // The map visualization step renders a WMTS base map in the browser. The client both fetches the
        // capabilities document (connect-src) and loads tile images (img-src) from the base map host, so
        // that origin has to be allow-listed. Defaults to the swisstopo base map; keep this in sync with
        // any override of the map visualization base map URL.
        // shortcut: one base map host per installation, derive the hosts from the pipeline definition if pipelines need different base maps
        var mapBaseMapUrl = configuration["MapVisualization:BaseMapWmtsCapabilitiesUrl"];
        if (string.IsNullOrWhiteSpace(mapBaseMapUrl))
        {
            mapBaseMapUrl = BaseMap.DefaultWmtsCapabilitiesUrl;
        }

        if (!Uri.TryCreate(mapBaseMapUrl, UriKind.Absolute, out var mapBaseMapUri))
            throw new InvalidOperationException($"MapVisualization:BaseMapWmtsCapabilitiesUrl '{mapBaseMapUrl}' is not a valid absolute URI.");
        var mapBaseMapOrigin = mapBaseMapUri.GetLeftPart(UriPartial.Authority);

        var connectSrcParts = new List<string> { "'self'", authorityOrigin };
        if (!string.IsNullOrWhiteSpace(blobEndpoint))
            connectSrcParts.Add(blobEndpoint);
        connectSrcParts.Add(mapBaseMapOrigin);
        var connectSrc = string.Join(' ', connectSrcParts);

        app.MapFallback(async context =>
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Response.Headers.Append(
                "Content-Security-Policy",
                $"default-src 'self'; script-src 'strict-dynamic' 'nonce-{nonce}'; style-src 'nonce-{nonce}'; img-src 'self' data: {mapBaseMapOrigin}; object-src 'none'; base-uri 'none'; connect-src {connectSrc}; form-action 'self'; frame-ancestors 'none'; require-trusted-types-for 'script';");
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(indexHtmlTemplate.Replace("__CSP_NONCE__", nonce));
        }).AllowAnonymous();
    }
}
