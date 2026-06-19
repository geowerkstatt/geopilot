using Geopilot.Api.Models;

namespace Geopilot.Api.Services;

/// <summary>
/// Provides methods for retrieving and managing mandates.
/// </summary>
public interface IMandateService
{
    /// <summary>
    /// Gets all mandates, optionally filtered by user and/or upload.
    /// </summary>
    /// <param name="user">If provided, only mandates this user can make deliveries for are returned.</param>
    /// <param name="uploadId">If provided, only mandates that accept the uploaded files' extensions are returned.</param>
    /// <returns>List of <see cref="Mandate"/> deliverable by the user for the upload.</returns>
    Task<List<Mandate>> GetMandatesAsync(User? user = null, Guid? uploadId = null);

    /// <summary>
    /// Retrieves the mandate with the specified id, if the specified user is allowed to access it.
    /// </summary>
    /// <param name="mandateId">The id of the mandate to retrieve.</param>
    /// <param name="user">The user that tries to access the mandate. If null, the user is considered unauthenticated.</param>
    /// <returns>The <see cref="Mandate"/> if found and accessible; otherwise, null.</returns>
    Task<Mandate?> GetMandateForUser(int mandateId, User? user);

    /// <summary>
    /// Retrieves a list of all file extensions that are accepted by any mandate in the system.
    /// </summary>
    /// <returns>A set of accepted file extensions defined by the mandates.</returns>
    HashSet<string> GetFileExtensionsForMandates();
}
