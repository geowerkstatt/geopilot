namespace GeoCop.Api.Contracts
{
    /// <summary>
    /// The validation settings response schema.
    /// </summary>
    public class ValidationSettingsResponse
    {
        /// <summary>
        /// File extensions that are allowed for upload.
        /// All entries start with a "." like ".txt", ".xml" and the collection can include ".*" (all files allowed).
        /// </summary>
        public ICollection<string> AllowedFileExtensions { get; set; } = new List<string>();
    }
}
