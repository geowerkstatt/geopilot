using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

internal class StepOutput
{
    public required OutputAction Action { get; set; }

    public required object Data { get; set; }
}
