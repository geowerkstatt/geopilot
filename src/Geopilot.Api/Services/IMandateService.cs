﻿using Geopilot.Api.Models;

namespace Geopilot.Api.Services;

/// <summary>
/// Provides methods for retrieving and managing mandates.
/// </summary>
public interface IMandateService
{
    /// <summary>
    /// Gets all mandates, optionally filtered by user and/or job.
    /// </summary>
    /// <param name="user">If provided, only mandates this user can make deliveries for are returned.</param>
    /// <param name="jobId">If provided, only mandates that accept the file type of the file of the job are returned.</param>
    /// <returns>List of <see cref="Mandate"/> deliverable by user and job.</returns>
    Task<List<Mandate>> GetMandatesAsync(User? user = null, Guid? jobId = null);

    /// <summary>
    /// Retrieves the mandate with the specified id, if the specified user is allowed to make deliveries for it
    /// and if the mandate accepts the file type of the file associated with the specified job.
    /// </summary>
    /// <param name="mandateId">The id of the mandate to retrieve.</param>
    /// <param name="user">The user that tries to access the mandate.</param>
    /// <param name="jobId">The job for which the mandate should be retrieved.</param>
    /// <returns>The <see cref="Mandate"/> if found and accessible; otherwise, null.</returns>
    Task<Mandate?> GetMandateByUserAndJobAsync(int mandateId, User user, Guid jobId);

    /// <summary>
    /// Retrieves a list of all file extensions that are accepted by any mandate in the system.
    /// </summary>
    /// <returns>A set of accepted file extensions defined by the mandates.</returns>
    HashSet<string> GetFileExtensionsForMandates();
}
