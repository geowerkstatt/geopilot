namespace Geopilot.Pipeline.Processes;

/// <summary>
/// The INTERLIS version of a transfer file. Every process that reports it uses these names, so a condition compares
/// against the same text whichever process recognized the version.
/// </summary>
internal enum InterlisVersion
{
    /// <summary>
    /// INTERLIS 1, transferred as ITF.
    /// </summary>
    Interlis1,

    /// <summary>
    /// INTERLIS 2.3, transferred as XTF.
    /// </summary>
    Interlis23,

    /// <summary>
    /// INTERLIS 2.4, transferred as XTF.
    /// </summary>
    Interlis24,
}
