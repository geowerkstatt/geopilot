using Geopilot.Api.Contracts;
using Geopilot.Api.Validation;

namespace Api;

/// <summary>
/// Provides extension methods for mapping domain models to DTOs.
/// </summary>
internal static class DtoMapperExtensions
{
    /// <summary>
    /// Maps a <see cref="ValidationJob"/> to a <see cref="ValidationJobResponse"/>.
    /// </summary>
    /// <param name="job">The validation job to map.</param>
    /// <returns>The mapped validation job response.</returns>
    public static ValidationJobResponse ToResponse(this ValidationJob job)
    {
        return new(
            job.Id,
            job.Status,
            job.MandateId,
            job.ValidatorResults.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToResponse()));
    }

    /// <summary>
    /// Maps a <see cref="ValidatorResult"/> to a <see cref="ValidatorResultResponse"/>.
    /// </summary>
    /// <param name="result">The validator result to map.</param>
    /// <returns>The mapped validator result response.</returns>
    public static ValidatorResultResponse ToResponse(this ValidatorResult result) =>
        new(result.Status, result.StatusMessage, result.LogFiles);
}
