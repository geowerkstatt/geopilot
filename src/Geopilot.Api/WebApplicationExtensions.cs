using Geopilot.Pipeline;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Processes.XtfErrorVisualization;
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

        var pipelineFactory = app.Services.GetRequiredService<IPipelineFactory>() as PipelineFactory;
        if (pipelineFactory == null)
        {
            throw new InvalidOperationException("PipelineFactory is not registered correctly.");
        }

        var validationErrors = pipelineFactory.PipelineProcessConfig.Validate();
        if (validationErrors.HasErrors)
        {
            throw new InvalidOperationException($"errors in pipeline definition:{Environment.NewLine}{validationErrors.ErrorMessage}");
        }

        var pipelineProcessFactory = app.Services.GetRequiredService<IPipelineProcessFactory>();

        var invalidProcessesErrors = new HashSet<string>();
        foreach (var pipeline in pipelineFactory.PipelineProcessConfig.Pipelines)
        {
            foreach (var step in pipeline.Steps)
            {
                try
                {
                    // Use Validate() rather than Build() so startup does not actually
                    // construct processes — invoking constructors here would leak
                    // per-step directories under $TEMP and allocate HttpClients /
                    // other per-process resources for every restart.
                    pipelineProcessFactory
                        .Builder()
                        .StepConfig(step)
                        .Processes(pipelineFactory.PipelineProcessConfig.Processes)
                        .Validate();
                }
                catch (Exception ex)
                {
                    invalidProcessesErrors.Add($"pipeline {pipeline.Id}, step {step.Id}, process {step.ProcessId}, error: {ex.Message}");
                }
            }
        }

        if (invalidProcessesErrors.Count > 0)
        {
            throw new InvalidOperationException($"Invalid pipeline processes found:{Environment.NewLine}{string.Join(Environment.NewLine, invalidProcessesErrors)}");
        }
    }

    /// <summary>
    /// Maps the SPA fallback route with CSP headers and nonce-based script/style injection.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app, nameof(app));
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var indexHtmlPath = !string.IsNullOrEmpty(app.Environment.WebRootPath) ? Path.Combine(app.Environment.WebRootPath, "index.html") : null;
        if (string.IsNullOrEmpty(indexHtmlPath) || !File.Exists(indexHtmlPath))
        {
            return;
        }

        var indexHtmlTemplate = File.ReadAllText(indexHtmlPath);
        var authorityOrigin = new Uri(configuration["Auth:Authority"]!).GetLeftPart(UriPartial.Authority);
        var blobEndpoint = configuration["CloudStorage:BlobEndpoint"];
        if (!string.IsNullOrWhiteSpace(blobEndpoint))
        {
            if (!Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var blobUri))
                throw new InvalidOperationException($"CloudStorage:BlobEndpoint '{blobEndpoint}' is not a valid absolute URI.");
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
