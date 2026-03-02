using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process;
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

        var pipelineProcessFactory = app.Services.GetRequiredService<IPipelineProcessFactory>();

        var invalidProcessesErrors = pipelineFactory.PipelineProcessConfig.Pipelines
            .SelectMany(p => p.Steps)
            .Select(s =>
            {
                object? process = null;
                try
                {
                    process = pipelineProcessFactory.CreateProcess(s, pipelineFactory.PipelineProcessConfig.Processes);
                    if (process == null)
                    {
                        return $"step {s.Id}, process {s.ProcessId}";
                    }
                }
                catch (Exception ex)
                {
                    return $"step {s.Id}, process {s.ProcessId}, error: {ex.Message}";
                }

                return null;
            })
            .Where(s => s != null)
            .ToHashSet();
        if (invalidProcessesErrors.Count > 0)
        {
            throw new InvalidOperationException($"Invalid pipeline processes found:{Environment.NewLine}{string.Join(Environment.NewLine, invalidProcessesErrors)}");
        }
    }
}
