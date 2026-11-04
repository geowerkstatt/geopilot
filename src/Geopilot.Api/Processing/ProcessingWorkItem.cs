using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// A pipeline ready to run together with the staged upload files it should process. Written to the
/// processing queue once a job's files have been staged, and consumed by the processing runner.
/// </summary>
/// <param name="Pipeline">The pipeline to run.</param>
/// <param name="Files">The uploaded files the run reads.</param>
/// <param name="IsDeclarable">Whether the run can become a delivery, see <see cref="ProcessingJob.IsDeclarable"/>.
/// Only then are its delivery files staged.</param>
public sealed record ProcessingWorkItem(IPipeline Pipeline, IReadOnlyList<IPipelineFile> Files, bool IsDeclarable);
