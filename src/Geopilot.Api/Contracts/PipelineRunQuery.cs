namespace Geopilot.Api.Contracts;

/// <summary>
/// Filters and pages the list of pipeline runs, whose rows are the attempts of one delivery each (see
/// <see cref="PipelineRunGroupResponse"/>). Every filter that is set narrows the list, and a run matches a filter
/// with several values when it matches one of them. Mandate and declarer are the same for all attempts of a delivery.
/// </summary>
public class PipelineRunQuery
{
    /// <summary>
    /// Only deliveries with an attempt started at or after this time (UTC), and before <see cref="To"/> if set.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Only deliveries with an attempt started before this time (UTC), and at or after <see cref="From"/> if set.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Only runs started for one of these mandates.
    /// </summary>
    public List<int> MandateIds { get; set; } = [];

    /// <summary>
    /// Only runs started by one of these persons, or by one of the <see cref="MachineClientIds"/>.
    /// </summary>
    public List<int> UserIds { get; set; } = [];

    /// <summary>
    /// Only runs started by one of these machine clients, or by one of the <see cref="UserIds"/>.
    /// </summary>
    public List<int> MachineClientIds { get; set; } = [];

    /// <summary>
    /// Only deliveries whose newest attempt is in one of these states.
    /// </summary>
    public List<PipelineRunStateFilter> States { get; set; } = [];

    /// <summary>
    /// The page to return, starting at 0.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// The number of runs per page, at most 100.
    /// </summary>
    public int PageSize { get; set; } = 25;
}

/// <summary>
/// The state a pipeline run is filtered by.
/// </summary>
public enum PipelineRunStateFilter
{
    /// <summary>
    /// The run ended successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The run ended with a warning.
    /// </summary>
    Warning,

    /// <summary>
    /// The run ended, but restricted its delivery.
    /// </summary>
    DeliveryRestriction,

    /// <summary>
    /// The run failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The run was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The run has no terminal state: it is still running, or its instance stopped while it ran.
    /// </summary>
    Open,
}
