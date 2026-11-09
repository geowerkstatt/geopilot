using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

/// <summary>
/// A file the process configuration names for a constructor parameter of type <see cref="IPipelineFile"/>, as the
/// process builder bound it, so the step can record which file its process was given.
/// </summary>
/// <param name="Parameter">The name of the constructor parameter.</param>
/// <param name="RelativePath">The configured path below the resources directory.</param>
/// <param name="File">The file the process received.</param>
internal sealed record ConfiguredFile(string Parameter, string RelativePath, IPipelineFile File);
