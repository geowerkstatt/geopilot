namespace Geopilot.Pipeline;

/// <summary>
/// Extension methods for <see cref="ProcessingState"/>.
/// </summary>
internal static class ProcessingStateExtensions
{
    /// <summary>
    /// Gets a value indicating whether a job that finished in this state may be delivered. Delivery is
    /// permitted for a successful run and for a run that completed with warnings; a run that restricts
    /// delivery, failed or was cancelled is not deliverable.
    /// </summary>
    /// <param name="state">The processing state to evaluate.</param>
    /// <returns><see langword="true"/> if the state permits delivery; otherwise <see langword="false"/>.</returns>
    public static bool IsDeliverable(this ProcessingState state) =>
        state is ProcessingState.Success or ProcessingState.Warning;
}
