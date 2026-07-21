using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// A pipeline ready to run together with the staged upload files it should process. Written to the
/// processing queue once a job's files have been staged, and consumed by the processing runner.
/// </summary>
public sealed record ProcessingWorkItem(IPipeline Pipeline, IReadOnlyList<IPipelineFile> Files);
