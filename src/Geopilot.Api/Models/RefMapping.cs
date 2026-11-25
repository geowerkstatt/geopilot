using System.ComponentModel.DataAnnotations.Schema;

namespace Geopilot.Api.Models;

/// <summary>
/// An entry of the reference data mapping, one object of the class <c>RefData</c> in the INTERLIS model
/// <c>IliVRefData_V1_0</c>. Tells ilivalidator which reference data to load for a specific scope.
/// </summary>
public class RefMapping
{
    /// <summary>
    /// The unique identifier for the ref data entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The value the pipeline reads from the delivery the entry applies to, e.g. the BFS number of a municipality.
    /// </summary>
    [Column(TypeName = "varchar(250)")]
    public required string Scope { get; set; }

    /// <summary>
    /// The reference data to load, each in the form <c>ilidata:&lt;Id&gt;</c>.
    /// </summary>
    public List<string> RefDataUris { get; set; } = [];

    /// <summary>
    /// A description of the entry.
    /// </summary>
    public string? Comment { get; set; }
}
