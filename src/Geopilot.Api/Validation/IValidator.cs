using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;

namespace Geopilot.Api.Validation;

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
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">If the validator has not been configured correctly and cannot do the validation.</exception>
    /// <exception cref="ValidationFailedException">If the validation failed unexpectedly.</exception>
    Task<ValidatorResult> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets all supported profiles of this validator.
    /// </summary>
    /// <returns>List of multilingual described <see cref="Profile"/> objects.</returns>
    Task<List<Profile>> GetSupportedProfilesAsync();
}
