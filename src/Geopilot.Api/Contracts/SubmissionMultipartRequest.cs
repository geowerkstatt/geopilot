using Geopilot.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The form of a machine delivery whose files come with the request, as the published API describes it. The
/// action reads the parts from the stream itself and never binds this type; it exists so the API document names
/// the fields and the files, which a generated client cannot learn from the code that reads them.
/// </summary>
public class SubmissionMultipartRequest
{
    /// <summary>
    /// The key of the mandate to deliver to. Compared exactly, including case. Sent before the files.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string MandateKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the delivery covers only part of the mandate. Required, optional or rejected, depending on the
    /// mandate. Sent before the files.
    /// </summary>
    public bool? PartialDelivery { get; set; }

    /// <summary>
    /// The delivery this one supersedes. Required, optional or rejected, depending on the mandate. Sent before
    /// the files.
    /// </summary>
    public int? PrecursorDeliveryId { get; set; }

    /// <summary>
    /// The comment accompanying the delivery. Required, optional or rejected, depending on the mandate. Sent
    /// before the files.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// The values of the metadata fields of the mandate as text: the same JSON object the <c>metadata</c> of the
    /// referencing route carries, with the field ids as keys. Sent before the files.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Your own identifier of the delivery, such as your delivery number. Send the same one with every attempt of
    /// the same delivery, so its attempts are recognized as one delivery. Optional. At most 128 characters, without
    /// control or invisible format characters; white space at either end is dropped, and nothing but white space
    /// counts as none. Never checked against earlier attempts. Sent before the files.
    /// </summary>
    [MaxLength(PipelineRun.ClientReferenceMaxLength)]
    public string? ClientReference { get; set; }

    /// <summary>
    /// The delivered files, after every field. The name of the part does not matter; a part counts as a file
    /// when it carries a file name.
    /// </summary>
    [Required]
    public IReadOnlyList<IFormFile> File { get; set; } = [];
}
