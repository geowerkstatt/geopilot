using System.ComponentModel.DataAnnotations.Schema;

namespace GeoCop.Api.Models
{
    /// <summary>
    /// A asset describes a file delivered or created by the validation and delivery process.
    /// </summary>
    internal class Asset
    {
        public int Id { get; set; }

        public byte[] FileHash { get; set; } = Array.Empty<byte>();

        public string OriginalFilename { get; set; } = string.Empty;

        public string SanitizedFilename { get; set; } = string.Empty;

        [Column(TypeName = "varchar(24)")]
        public AssetType AssetType { get; set; } = AssetType.PrimaryData;

        public Delivery Delivery { get; set; } = new Delivery();
    }
}
