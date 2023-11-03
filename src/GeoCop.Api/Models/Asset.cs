using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoCop.Api.Models
{
    internal class Asset
    {
        [Key]
        public string FileHash { get; set; } = string.Empty;

        public string OriginalFilename { get; set; } = string.Empty;

        public string SanitizedFilename { get; set; } = string.Empty;

        [Column(TypeName = "varchar(24)")]
        public AssetType AssetType { get; set; } = AssetType.PrimaryData;

        public Delivery Delivery { get; set; } = new Delivery();
    }
}
