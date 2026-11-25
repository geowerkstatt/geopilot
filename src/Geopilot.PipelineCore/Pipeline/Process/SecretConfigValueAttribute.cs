namespace Geopilot.PipelineCore.Pipeline.Process;

/// <summary>
/// Marks a constructor parameter of a process or a channel that takes a secret, such as a token or a password. Its
/// value comes from the base configuration of the installation only: a pipeline definition that sets it fails the
/// start, and a mandate cannot supply it. Wherever geopilot records the configuration, in the execution protocol or
/// the definition snapshot of a run, a value that is set appears as <c>**********</c>. Only a parameter of type
/// <see cref="string"/> may carry it. The constructor must not name the value in an exception, because the message of
/// a failed attempt is kept in the protocol.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SecretConfigValueAttribute : Attribute
{
}
