namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// The reference data mapping the host binds to a job, see <see cref="Process.PipelineJobBindings"/>. A process receives it
/// through a constructor parameter of this type: <see langword="null"/> where the installation does not maintain the
/// mapping, otherwise every entry, possibly none.
/// </summary>
internal interface IRefMappings
{
    /// <summary>
    /// The entries as the host read them when the job started.
    /// </summary>
    IReadOnlyList<RefMappingEntry> Entries { get; }
}
