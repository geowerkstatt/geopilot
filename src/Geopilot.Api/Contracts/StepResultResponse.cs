using Geopilot.Api.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a single processing step's result.
/// </summary>
/// <param name="Id">The unique id of the step within the pipeline.</param>
/// <param name="Name">The step's display name, keyed by ISO 639 language code.</param>
/// <param name="State">The current state of the step.</param>
/// <param name="StatusMessage">Localized status message (key: ISO 639 language code) produced by the step (e.g. condition-driven skip/fail messages, validator summaries). <see langword="null"/> when no status message was emitted.</param>
/// <param name="Downloads">Mapping of output key to absolute download URL for files produced by the step.</param>
public record StepResultResponse(
    string Id,
    IDictionary<string, string> Name,
    StepState State,
    IDictionary<string, string>? StatusMessage,
    IDictionary<string, Uri> Downloads);
