using System.ComponentModel.DataAnnotations;

namespace GeoCop.Api
{
    /// <summary>
    /// The upload response schema.
    /// </summary>
    public class UploadResponse
    {
        /// <summary>
        /// The job identification.
        /// </summary>
        [Required]
        public Guid JobId { get; set; }

        /// <summary>
        /// The status url.
        /// </summary>
        [Required]
        public Uri? StatusUrl { get; set; }
    }
}
