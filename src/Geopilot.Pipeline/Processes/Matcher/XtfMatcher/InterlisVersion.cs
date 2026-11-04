namespace Geopilot.Pipeline.Processes.Matcher.XtfMatcher;

/// <summary>
/// The INTERLIS version of a transfer file, as the <see cref="XtfMatcherProcess"/> recognizes it.
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
