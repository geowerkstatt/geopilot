using GeoCop.Api.Exceptions;
using System.Globalization;
using System.Threading.Tasks.Dataflow;

namespace GeoCop.Api
{
    /// <summary>
    /// Provides extension methods which can be reused from different locations.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets the sanitized file extension for the specified <paramref name="unsafeFileName"/>.
        /// </summary>
        /// <param name="unsafeFileName">The unsafe file name.</param>
        /// <param name="acceptedFileExtensions">The accepted file extensions.</param>
        /// <returns>The sanitized file extension for the specified <paramref name="unsafeFileName"/>.</returns>
        /// <exception cref="UnknownExtensionException">If file extension of <paramref name="unsafeFileName"/> is unknown.</exception>
        public static string GetSanitizedFileExtension(this string unsafeFileName, IEnumerable<string> acceptedFileExtensions)
        {
            try
            {
                return acceptedFileExtensions
                    .Single(extension => Path.GetExtension(unsafeFileName).Equals(extension, StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidOperationException)
            {
                var invalidFileExtension = Path.GetExtension(unsafeFileName);
                throw new UnknownExtensionException(
                    invalidFileExtension,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Transfer file extension <{0}> is an unknown file extension.",
                        invalidFileExtension));
            }
        }

        /// <summary>
        /// Gets the log file for the specified <paramref name="logType"/>.
        /// </summary>
        /// <param name="fileProvider">The <see cref="IFileProvider"/> which provides read/write access to a predefined folder.</param>
        /// <param name="logType">The log type to download.</param>
        /// <returns>The log file for the specified <paramref name="logType"/>.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="fileProvider"/> is <c>null</c>.</exception>
        /// <exception cref="FileNotFoundException">If the log file could not be found.</exception>
        public static string GetLogFile(this IFileProvider fileProvider, LogType logType)
        {
            if (fileProvider == null) throw new ArgumentNullException(nameof(fileProvider));

            try
            {
                return fileProvider.GetFiles().Where(x => x.EndsWith($"_log.{logType}", StringComparison.OrdinalIgnoreCase)).Single();
            }
            catch (InvalidOperationException)
            {
                throw new FileNotFoundException(
                    string.Format(CultureInfo.InvariantCulture, "Log file of type <{0}> not found in <{1}>", logType, fileProvider.HomeDirectory));
            }
        }

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
