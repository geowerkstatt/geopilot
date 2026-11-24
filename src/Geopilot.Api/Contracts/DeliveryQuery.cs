using Geopilot.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Filters and pages the list of deliveries of the administration. Every filter that is set narrows the list, and a
/// delivery matches a filter with several values when it matches one of them.
/// </summary>
public class DeliveryQuery
{
    // As many as for the recordings: more than anyone chooses in the administration, but bounded, since each value
    // goes into the query.
    private const int MaxFilterValues = 100;

    /// <summary>
    /// Only deliveries made at or after this time (UTC), and before <see cref="To"/> if set.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Only deliveries made before this time (UTC), and at or after <see cref="From"/> if set.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Only deliveries for one of these mandates. At most 100.
    /// </summary>
    [MaxLength(MaxFilterValues)]
    public List<int> MandateIds { get; set; } = [];

    /// <summary>
    /// Only deliveries made by one of these persons, or by one of the <see cref="MachineClientIds"/>. At most 100.
    /// </summary>
    [MaxLength(MaxFilterValues)]
    public List<int> UserIds { get; set; } = [];

    /// <summary>
    /// Only deliveries made by one of these machine clients, or by one of the <see cref="UserIds"/>. At most 100.
    /// </summary>
    [MaxLength(MaxFilterValues)]
    public List<int> MachineClientIds { get; set; } = [];

    /// <summary>
    /// Only deliveries with a reaction of one of these kinds that gave up (<see cref="ReactionState.Error"/>), whose
    /// column in the list therefore shows <see cref="ReactionState.Error"/> too. Unlike the other states, a reaction stays
    /// on it until an administrator resets it. At most 100.
    /// </summary>
    [MaxLength(MaxFilterValues)]
    public List<ReactionKind> FailedReactionKinds { get; set; } = [];

    /// <summary>
    /// Only deliveries whose id, written out, begins with these 1 to 9 digits: <c>12</c> matches 12, 120 and 1234, but
    /// not 312.
    /// </summary>
    [RegularExpression("^[0-9]{1,9}$")]
    public string? IdPrefix { get; set; }

    /// <summary>
    /// The page to return, from 0 to 1,000,000, which keeps the offset of the page within a whole number.
    /// </summary>
    [Range(0, 1_000_000)]
    public int Page { get; set; }

    /// <summary>
    /// The number of deliveries per page, from 1 to 100.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}
