using Geopilot.Api.Pipeline;
using Microsoft.EntityFrameworkCore;

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
    }
}
