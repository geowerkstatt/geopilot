using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Test.Processes;

/// <summary>
/// Test process with a file valued configuration parameter: it names a file the deployment ships, resolved against
/// the resources directory, and is therefore configuration and not step input.
/// </summary>
public class ResourceFileTestProcess
{
    public ResourceFileTestProcess(IPipelineFile? resource)
    {
        this.Resource = resource;
    }

    public IPipelineFile? Resource { get; }

    [PipelineProcessRun]
    public Task<ResourceFileTestResult> RunAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ResourceFileTestResult { ResourceName = Resource?.OriginalFileName });
    }
}

/// <summary>
/// Result of the <see cref="ResourceFileTestProcess"/>.
/// </summary>
public class ResourceFileTestResult
{
    public string? ResourceName { get; init; }
}
