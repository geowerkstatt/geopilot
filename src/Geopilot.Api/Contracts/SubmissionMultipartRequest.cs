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
    /// The delivered files, after every field. The name of the part does not matter; a part counts as a file
    /// when it carries a file name.
    /// </summary>
    [Required]
    public IReadOnlyList<IFormFile> File { get; set; } = [];
}
