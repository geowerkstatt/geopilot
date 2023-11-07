namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Provides methods to validate files.
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Gets the identifier for this instance.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the name of the file to validate.
        /// </summary>
        string? File { get; }

        /// <summary>
        /// Asynchronously validates the <paramref name="file"/> specified.
        /// The file must be accessible by the <see cref="IFileProvider"/> when executing this function.
        /// </summary>
        /// <param name="file">The name of the file to validate.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="file"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="file"/> is <c>string.Empty</c>.</exception>
        /// <exception cref="InvalidOperationException">If <paramref name="file"/> is not found.</exception>
        /// <exception cref="ValidationFailedException">If the validation of the <paramref name="file"/> failed unexpectedly.</exception>
        Task<ValidationJobStatus> ExecuteAsync(string file, CancellationToken cancellationToken);
    }
}
