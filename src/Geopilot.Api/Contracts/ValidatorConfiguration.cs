using Geowerkstatt.Ilicop.Web.Contracts;

namespace Geopilot.Api.Contracts
{
    /// <summary>
    /// A configuration for a validator endpoint.
    /// </summary>
    public record ValidatorConfiguration
    {
        /// <summary>
        /// List of supported file extensions for the validator.
        /// All entries start with a "." like ".txt", ".xml" and the collection can include ".*" (all files allowed).
        /// </summary>
        public List<string> SupportedFileExtensions { get; init; } = new();

        /// <summary>
        /// Profile definitions supported by the validator.
        /// </summary>
        public List<Profile> Profiles { get; init; } = new();
    }
}
