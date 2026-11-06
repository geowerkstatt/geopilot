using Geopilot.Api.Models;
using Geopilot.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// One page of pipeline runs, newest first.
/// </summary>
/// <param name="TotalCount">How many runs match the filters, across all pages.</param>
/// <param name="Items">The runs on this page.</param>
public record PipelineRunPageResponse(int TotalCount, List<PipelineRunSummaryResponse> Items);

/// <summary>
/// A pipeline run as the list shows it.
/// </summary>
/// <param name="JobId">The id of the processing job.</param>
/// <param name="StartedAt">When the job was started (UTC).</param>
/// <param name="TerminalAt">When the job reached its terminal state (UTC).</param>
/// <param name="Declarer">Who started the run, or <see langword="null"/> when it was started without signing in.</param>
/// <param name="ClientKind">The kind of client that started the run.</param>
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
    MandateReference? Mandate,
    string PipelineId,
    ProcessingState? TerminalState,
    PipelineRunOutcome Outcome);
