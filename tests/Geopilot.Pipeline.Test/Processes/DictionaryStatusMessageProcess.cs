using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Test.Processes;

internal class DictionaryStatusMessageProcess
{
    [PipelineProcessRun]
    public Task<DictionaryStatusMessageResult> RunAsync() => Task.FromResult(new DictionaryStatusMessageResult());
}

public class DictionaryStatusMessageResult
{
    public IReadOnlyDictionary<string, string> ReadOnlyStatusMessage { get; init; } = new Dictionary<string, string>();

    public IDictionary<string, string> MutableStatusMessage { get; init; } = new Dictionary<string, string>();
}
