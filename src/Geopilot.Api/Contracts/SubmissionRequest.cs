using Geopilot.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Geopilot.Api.Contracts;

/// <summary>
/// Starts a machine delivery for files that were already uploaded. This is the shape an installation that
/// stores uploads in the cloud expects, where the caller obtains the upload and its URLs beforehand.
/// </summary>
public class SubmissionRequest
{
    /// <summary>
    /// The key of the mandate to deliver to. Compared exactly, including case.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string MandateKey { get; set; } = string.Empty;

    /// <summary>
    /// The upload whose files are delivered.
    /// </summary>
    [Required]
    public Guid UploadId { get; set; }

    /// <summary>
    /// Whether the delivery covers only part of the mandate. Required, optional or rejected, depending on the
    /// mandate.
    /// </summary>
    public bool? PartialDelivery { get; set; }

    /// <summary>
    /// The delivery this one supersedes. Required, optional or rejected, depending on the mandate.
    /// </summary>
    public int? PrecursorDeliveryId { get; set; }

    /// <summary>
    /// The comment accompanying the delivery. Required, optional or rejected, depending on the mandate.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// The values of the metadata fields of the mandate, keyed by field id. Which fields are required follows the
    /// mandate.
    /// </summary>
    public Dictionary<string, JsonElement>? Metadata { get; set; }

    /// <summary>
    /// Your own identifier of the delivery, such as your delivery number. Send the same one with every attempt of
    /// the same delivery, so its attempts are recognized as one delivery. Optional. At most 128 characters, without
    /// control or invisible format characters; white space at either end is dropped, and nothing but white space
    /// counts as none. Never checked against earlier attempts.
    /// </summary>
    [MaxLength(PipelineRun.ClientReferenceMaxLength)]
    public string? ClientReference { get; set; }
}
