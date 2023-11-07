namespace GeoCop.Api.Models
{
    /// <summary>
    /// Meta information on how an asset was created and how it has to be interpreted.
    /// </summary>
    public enum AssetType
    {
        /// <summary>
        /// Primary data delivered by the user.
        /// </summary>
        PrimaryData,

        /// <summary>
        /// Reports created by the validation process.
        /// </summary>
        ValidationReport,

        /// <summary>
        /// Metadata created by the declaration or validation process.
        /// </summary>
        Metadata,
    }
}
