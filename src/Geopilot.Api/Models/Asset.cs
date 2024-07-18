using System.ComponentModel.DataAnnotations.Schema;

namespace Geopilot.Api.Models;

/// <summary>
/// An asset describes a file delivered or created by the validation and delivery process.
/// </summary>
public class Asset
{
    /// <summary>
    /// The unique identifier for the asset.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The SH256 hash of the file contents.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[] FileHash { get; set; } = Array.Empty<byte>();
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// The original filename of the file.
    /// </summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>
    /// A filename that is safe to use in a file system.
    /// </summary>
    public string SanitizedFilename { get; set; } = string.Empty;

    /// <summary>
    /// The type of the asset.
    /// </summary>
    [Column(TypeName = "varchar(24)")]
    public AssetType AssetType { get; set; } = AssetType.PrimaryData;

    /// <summary>
    /// Backreference to the delivery the asset belongs to.
    /// </summary>
    public Delivery? Delivery { get; set; } = new Delivery();

    /// <summary>
    /// The deletion status of the asset.
    /// </summary>
    public bool Deleted { get; set; }
}
