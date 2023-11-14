namespace GeoCop.Api.Contracts
{
    /// <summary>
    /// The validation settings response schema.
    /// </summary>
    public class ValidationSettingsResponse
    {
        /// <summary>
        /// File extensions that are allowed for upload.
        /// </summary>
        public ICollection<string> AllowedFileExtensions { get; set; } = new List<string>();
    }
}
