using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

/// <summary>
/// Context for a pipeline execution, containing the results of each step.
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Gets or sets the list of files to be uploaded to the pipeline.
    /// </summary>
    /// <remarks>This property allows the user to specify multiple files for upload. Ensure that the list is
    /// not empty before initiating the upload process.</remarks>
    public required IReadOnlyList<IPipelineFile> Upload { get; set; }

    /// <summary>
    /// The results of each step in the pipeline.
    /// </summary>
    public required Dictionary<string, StepResult> StepResults { get; set; }

    /// <summary>
    /// Gets or sets the localized delivery restriction message.
    /// If delivery is restricted by one or more conditions, contains the merged localized message; otherwise <see langword="null"/>.
    /// </summary>
    public LocalizedText? DeliveryRestrictionMessage { get; set; }
}
