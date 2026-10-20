namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// The format of the transfer file(s) of an ili2gpkg operation.
/// </summary>
public enum Ili2GpkgTransferFormat
{
    /// <summary>
    /// INTERLIS 2 transfer format (<c>.xtf</c>).
    /// </summary>
    Xtf,

    /// <summary>
    /// INTERLIS 1 transfer format (<c>.itf</c>).
    /// </summary>
    Itf,

    /// <summary>
    /// GML transfer format (eCH-0118-1.0), only supported for export.
    /// </summary>
    Gml,

    /// <summary>
    /// GML 2.0 transfer format (eCH-0118-2.0), only supported for export.
    /// </summary>
    Gml20,
}
