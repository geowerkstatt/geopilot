using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Names a mandate without describing it, for answers that only point at one.
/// </summary>
/// <param name="Id">The unique identifier of the mandate.</param>
/// <param name="Name">The display name of the mandate.</param>
public record MandateReference(int Id, LocalizedText Name);
