using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

/// <summary>
/// Represents an initiated upload: a set of files the client uploads to cloud storage, identified by
/// <see cref="Id"/>, before it is turned into a <see cref="ProcessingJob"/> by starting a job.
/// </summary>
public record UploadInfo(Guid Id, ImmutableList<CloudFileInfo> Files, DateTime CreatedAt);
