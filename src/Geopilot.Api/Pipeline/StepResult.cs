using static Org.BouncyCastle.Math.Primes;

namespace Geopilot.Api.Pipeline;

internal class StepResult
{
    public required StepState State { get; set; }

    public Dictionary<string, StepOutput> Outputs { get; set; } = new Dictionary<string, StepOutput>();
}
