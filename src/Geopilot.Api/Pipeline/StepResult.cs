using static Org.BouncyCastle.Math.Primes;

namespace Geopilot.Api.Pipeline;

internal class StepResult
{
    public required StepState State { get; set; }

    public required Dictionary<string, StepOutput> Outputs { get; set; }
}
