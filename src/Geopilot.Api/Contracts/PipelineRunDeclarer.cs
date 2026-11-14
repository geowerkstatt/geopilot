namespace Geopilot.Api.Contracts;

/// <summary>
/// Whether a person or a machine client started a pipeline run.
/// </summary>
public enum DeclarerKind
{
    /// <summary>
    /// A signed in person.
    /// </summary>
    Person,

    /// <summary>
    /// A registered machine client.
    /// </summary>
    MachineClient,
}

/// <summary>
/// Who started a pipeline run. A run started without signing in has none.
/// </summary>
/// <param name="Kind">Whether a person or a machine client started the run.</param>
/// <param name="Id">The id of the person or the machine client.</param>
/// <param name="Name">The display name of the person or the machine client.</param>
public record PipelineRunDeclarer(DeclarerKind Kind, int Id, string Name);
