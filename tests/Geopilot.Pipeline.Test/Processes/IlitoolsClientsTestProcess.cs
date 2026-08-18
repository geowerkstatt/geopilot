using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;

namespace Geopilot.Pipeline.Test.Processes;

/// <summary>
/// Test process whose constructor takes both ilitools clients. The runtime supplies them, so neither may be
/// demanded as a configured value while a step is validated.
/// </summary>
public class IlitoolsClientsTestProcess
{
    public IlitoolsClientsTestProcess(IIli2GpkgClient ili2GpkgClient, IIlivalidatorClient ilivalidatorClient, ILogger logger)
    {
        this.Ili2GpkgClient = ili2GpkgClient;
        this.IlivalidatorClient = ilivalidatorClient;
        this.Logger = logger;
    }

    public IIli2GpkgClient Ili2GpkgClient { get; }

    public IIlivalidatorClient IlivalidatorClient { get; }

    public ILogger Logger { get; }

    [PipelineProcessRun]
    public Task<IlitoolsClientsTestResult> RunAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new IlitoolsClientsTestResult());
    }
}

/// <summary>
/// Result of the <see cref="IlitoolsClientsTestProcess"/>.
/// </summary>
public class IlitoolsClientsTestResult
{
    public bool Validated { get; init; }
}
