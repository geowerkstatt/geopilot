using Geopilot.Api.Contracts;

namespace Geopilot.Api.Validation;

/// <summary>
/// Defines the contract for a validator that performs validation operations on files or data.
/// </summary>
/// <remarks>Implementations of this interface are expected to be configurable with the file and/or data to be validated and everything else required to run the validation.</remarks>
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
    /// Executes the validation process asynchronously.
    /// </summary>
    /// <remarks>This method must be called after the validator has been configured with the necessary file and/or data to be validated.</remarks>
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
