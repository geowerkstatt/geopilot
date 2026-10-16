using Geopilot.Pipeline;

namespace Geopilot.Api.Processing;

/// <summary>
/// How a run ended, as the runner reports it to <see cref="JobLifecycle.RunFinishedAsync"/>. The lifecycle derives
/// the job's new state and every consequence of the run from it, so the reporter only states what happened.
/// </summary>
public abstract record RunOutcome
{
    // Private, so only the cases below can derive and the set of outcomes stays closed.
    private RunOutcome()
    {
    }

    /// <summary>
    /// The pipeline ran to its end and reported <paramref name="State"/>. A <see cref="ProcessingState.Failed"/>
    /// here means a condition failed the data, unlike <see cref="Crashed"/>.
    /// </summary>
    public sealed record Completed(ProcessingState State) : RunOutcome;

    /// <summary>
    /// The run exceeded the job timeout, whatever state the pipeline reported until then.
    /// </summary>
    public sealed record TimedOut : RunOutcome;

    /// <summary>
    /// The run was cut short because the host shut down.
    /// </summary>
    public sealed record ShutDown : RunOutcome;

    /// <summary>
    /// An exception aborted the run: a step's process threw, or persisting the downloads or staging the delivery
    /// files failed. <paramref name="Reason"/> is the message that goes into the execution protocol.
    /// </summary>
    public sealed record Crashed(string Reason) : RunOutcome;
}
