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

        var invalidProcessesErrors = new HashSet<string>();
        foreach (var pipeline in pipelineFactory.PipelineProcessConfig.Pipelines)
        {
            foreach (var step in pipeline.Steps)
            {
                try
                {
                    pipelineProcessFactory.CreateProcess(step, pipelineFactory.PipelineProcessConfig.Processes);
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
}
