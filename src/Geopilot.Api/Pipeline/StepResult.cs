using static Org.BouncyCastle.Math.Primes;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents the result of a single step in a pipeline.
/// </summary>
public class StepResult
{
    /// <summary>
    /// The outputs produced by this step.
    /// </summary>
    public Dictionary<string, StepOutput> Outputs { get; set; } = new Dictionary<string, StepOutput>();
}
