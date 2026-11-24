using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Pipeline.Process;

// In the namespace of the built-in processes, because a plugin loads its own copy of Geopilot.Pipeline and so never
// receives the reference data mapping; the factory resolves built-in processes from the app domain.
namespace Geopilot.Pipeline.Processes.RefMappingTest;

/// <summary>
/// Test process that takes the reference data mapping the host binds to a job. The parameter is not nullable, so a
/// step validates only because the runtime knows the type, not because it may leave the parameter empty.
/// </summary>
internal class RefMappingTestProcess
{
    public RefMappingTestProcess(IRefMappings refMappings)
    {
        RefMappings = refMappings;
    }

    public IRefMappings RefMappings { get; }

    [PipelineProcessRun]
    public Task<RefMappingTestResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(new RefMappingTestResult());
}

/// <summary>
/// Result of the <see cref="RefMappingTestProcess"/>.
/// </summary>
internal class RefMappingTestResult
{
}
