using System.Threading.Tasks.Dataflow;

namespace GeoCop.Api
{
    /// <summary>
    /// Provides extension methods which can be reused from different locations.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Asynchronously invokes the specified <paramref name="action"/> on the given items in <paramref name="source"/>.
        /// </summary>
        /// <typeparam name="T">The type of the items in the sequence.</typeparam>
        /// <param name="source">The asynchronous sequence containing the items.</param>
        /// <param name="action">The action to invoke.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
        public static async Task ParallelForEachAsync<T>(this IAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken cancellationToken = default)
        {
            var dataFlowBlockOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                CancellationToken = cancellationToken,
            };

            var actionBlock = new ActionBlock<T>(action, dataFlowBlockOptions);
            await foreach (var item in source)
            {
                await actionBlock.SendAsync(item);
            }

            actionBlock.Complete();
            await actionBlock.Completion;
        }
    }
}
