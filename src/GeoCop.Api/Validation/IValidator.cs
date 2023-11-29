using GeoCop.Api.FileAccess;

namespace GeoCop.Api.Validation;

/// <summary>
/// Provides methods to validate a <see cref="ValidationJob"/>.
/// </summary>
public interface IValidator
{
    /// <summary>
    /// Gets the name of the validator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the supported file extensions.
    /// </summary>
    Task<ICollection<string>> GetSupportedFileExtensionsAsync();

    /// <summary>
    /// Asynchronously validates the <paramref name="validationJob"/> specified.
    /// Its file must be accessible by an <see cref="IFileProvider"/> when executing this function.
    /// </summary>
    /// <param name="validationJob">The validation job.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="validationJob"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">If the file of the <paramref name="validationJob"/> is <c>string.Empty</c>.</exception>
    /// <exception cref="InvalidOperationException">If the file of the <paramref name="validationJob"/> is not found.</exception>
    /// <exception cref="ValidationFailedException">If the validation failed unexpectedly.</exception>
    Task<ValidatorResult> ExecuteAsync(ValidationJob validationJob, CancellationToken cancellationToken);
}
