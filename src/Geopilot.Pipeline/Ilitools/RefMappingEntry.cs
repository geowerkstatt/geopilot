namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// An entry of the reference data mapping (<see cref="IRefMappings"/>), one object of the class <c>RefData</c> in the
/// INTERLIS model <c>IliVRefData_V1_0</c>.
/// </summary>
/// <param name="Scope">The value the pipeline reads from the delivery the entry applies to, e.g. the BFS number of a municipality.</param>
/// <param name="RefDataUris">The reference data to load, each in the form <c>ilidata:&lt;Id&gt;</c>.</param>
/// <param name="Comment">A description of the entry.</param>
internal sealed record RefMappingEntry(string Scope, IReadOnlyList<string> RefDataUris, string? Comment);
