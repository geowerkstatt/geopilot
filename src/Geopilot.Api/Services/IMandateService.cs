using Geopilot.Api.Contracts;
using Geopilot.Api.Models;

namespace Geopilot.Api.Services;

/// <summary>
/// Provides methods for retrieving and managing mandates.
/// </summary>
public interface IMandateService
{
    /// <summary>
    /// Gets all mandates.
    /// </summary>
    /// <returns>List of all <see cref="Mandate"/>.</returns>
    Task<List<Mandate>> GetMandatesAsync();

    /// <summary>
    /// Gets all mandates, filtered by user and upload.
    /// </summary>
    /// <param name="user">Only mandates this user can make deliveries for are returned.</param>
    /// <param name="uploadId">Only mandates that accept the uploaded files' extensions are returned. Pass
    /// <see langword="null"/> to skip that filter, for a caller that has not uploaded anything yet.</param>
    /// <returns>List of <see cref="MandateSummary"/> deliverable by the user for the upload.</returns>
    Task<List<MandateSummary>> GetMandateSummariesAsync(User? user, Guid? uploadId);

    /// <summary>
    /// Gets the unique keys of all mandates.
    /// </summary>
    /// <returns>List of mandate keys.</returns>
    Task<List<string>> GetMandateKeysAsync();

    /// <summary>
    /// Retrieves the mandate with the specified id, if the specified user is allowed to access it.
    /// </summary>
    /// <param name="mandateId">The id of the mandate to retrieve.</param>
    /// <param name="user">The user that tries to access the mandate. If null, the user is considered unauthenticated.</param>
    /// <returns>The <see cref="Mandate"/> if found and accessible; otherwise, null.</returns>
    Task<Mandate?> GetMandateForUser(int mandateId, User? user);

    /// <summary>
    /// Retrieves the mandate with the specified key, if the specified user is allowed to access it.
    /// </summary>
    /// <param name="key">The unique key of the mandate. Compared exactly, including case.</param>
    /// <param name="user">The user that tries to access the mandate. If null, the user is considered unauthenticated.</param>
    /// <returns>The <see cref="Mandate"/> if found and accessible; otherwise, null.</returns>
    Task<Mandate?> GetMandateByKeyForUser(string key, User? user);

    /// <summary>
    /// Checks whether the mandate can take a delivery, and if not, what stands in the way.
    /// </summary>
    /// <param name="mandate">The mandate to check.</param>
    /// <param name="uploadId">The upload whose file types must be accepted, or <see langword="null"/> to skip
    /// that part of the check.</param>
    /// <exception cref="ArgumentException">If no upload with the specified <paramref name="uploadId"/> exists.</exception>
    Task<MandateDeliverability> GetDeliverabilityAsync(Mandate mandate, Guid? uploadId);

    /// <summary>
    /// Checks whether the mandate accepts the given file extension.
    /// </summary>
    /// <param name="mandateId">The id of the mandate.</param>
    /// <param name="fileExtension">The extension including the leading period.</param>
    Task<bool> AcceptsFileExtensionAsync(int mandateId, string fileExtension);

    /// <summary>
    /// Retrieves a list of all file extensions that are accepted by any mandate in the system.
    /// </summary>
    /// <returns>A set of accepted file extensions defined by the mandates.</returns>
    HashSet<string> GetFileExtensionsForMandates();
}
