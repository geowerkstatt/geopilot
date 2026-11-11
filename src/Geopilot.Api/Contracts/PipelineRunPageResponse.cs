using Geopilot.Api.Models;
using Geopilot.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// One page of pipeline runs, grouped by delivery and newest first.
/// </summary>
/// <param name="TotalCount">How many groups match the filters, across all pages.</param>
/// <param name="Items">The groups on this page.</param>
public record PipelineRunPageResponse(int TotalCount, List<PipelineRunGroupResponse> Items);

/// <summary>
/// The attempts of one delivery: the runs a client started with the same client reference, sender and mandate. A run
/// without client reference is a group of its own. A group counts as one row of the list.
/// </summary>
/// <param name="Latest">The newest attempt, which the row shows and the state filter judges.</param>
/// <param name="AttemptCount">How many attempts the delivery had, the newest included, whether or not they lie in the period.</param>
public record PipelineRunGroupResponse(PipelineRunSummaryResponse Latest, int AttemptCount);

/// <summary>
/// A pipeline run as the list shows it.
/// </summary>
/// <param name="JobId">The id of the processing job.</param>
/// <param name="StartedAt">When the job was started (UTC).</param>
/// <param name="TerminalAt">When the job reached its terminal state (UTC).</param>
/// <param name="Declarer">Who started the run, or <see langword="null"/> when it was started without signing in.</param>
/// <param name="ClientKind">The kind of client that started the run.</param>
/// <param name="ClientReference">The client's own identifier of the delivery the run is an attempt of, or <see langword="null"/> when it carried none.</param>
/// <param name="Mandate">The mandate the run was started for, when it still exists.</param>
/// <param name="PipelineId">The id of the pipeline that ran.</param>
/// <param name="TerminalState">The state the job ended in, or <see langword="null"/> while it runs or when the outcome is unknown.</param>
/// <param name="Outcome">What became of the run.</param>
public record PipelineRunSummaryResponse(
    Guid JobId,
    DateTime StartedAt,
    DateTime? TerminalAt,
    PipelineRunDeclarer? Declarer,
    ClientKind ClientKind,
    string? ClientReference,
    MandateReference? Mandate,
    string PipelineId,
    ProcessingState? TerminalState,
    PipelineRunOutcome Outcome);
