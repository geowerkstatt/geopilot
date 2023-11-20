namespace GeoCop.Api.Validation.Interlis
{
    /// <summary>
    /// Result of a settings query of interlis-check-service at /api/v1/settings.
    /// </summary>
    public class IliCheckSettingsResponse
    {
        /// <summary>
        /// The accepted file types.
        /// </summary>
        public string? AcceptedFileTypes { get; set; }
    }
}
