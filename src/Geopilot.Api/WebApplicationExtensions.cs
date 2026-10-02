using Geopilot.Pipeline;
using Geopilot.Pipeline.Processes.XtfErrorVisualization;
using Microsoft.Extensions.FileProviders;
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
        if (!string.IsNullOrEmpty(overrideDirectory) && Directory.Exists(overrideDirectory))
        {
            app.Environment.WebRootFileProvider = new CompositeFileProvider(
                new PhysicalFileProvider(Path.GetFullPath(overrideDirectory)),
                app.Environment.WebRootFileProvider);
            app.Logger.LogInformation("Serving public assets from {PublicAssetsOverride} in front of the web root.", overrideDirectory);
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
        var mapBaseMapUrl = configuration["MapVisualization:BaseMapWmtsCapabilitiesUrl"];
        if (string.IsNullOrWhiteSpace(mapBaseMapUrl))
        {
            mapBaseMapUrl = MapVisualizationBuilder.DefaultBaseMapWmtsCapabilitiesUrl;
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
