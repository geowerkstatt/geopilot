using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Request to start a job.
/// </summary>
public class StartJobRequest
{
    /// <summary>
    /// The id of the mandate the job should be started with.
    /// </summary>
    [Required]
    public int MandateId { get; set; }
}
