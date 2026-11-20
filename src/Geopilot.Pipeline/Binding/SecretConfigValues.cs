using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Reflection;
using System.Text.Json;

namespace Geopilot.Pipeline;

/// <summary>
/// Keeps the values of parameters marked <see cref="SecretConfigValueAttribute"/> out of what geopilot records, the
/// definition snapshot of a run and the configuration of a delivery attempt. Masked when written, so no reader of the
/// protocol, be it the API, a backup or a query, has anything to hide.
/// </summary>
internal static class SecretConfigValues
{
    /// <summary>
    /// What a secret that is set reads as. Always the same, so it tells nothing about the value.
    /// </summary>
    internal const string Mask = "**********";

    internal static bool IsSecret(ParameterInfo parameter) => parameter.IsDefined(typeof(SecretConfigValueAttribute));

    /// <summary>
    /// The value as the protocol keeps it: <see cref="Mask"/> for a secret that is set, any other value unchanged.
    /// </summary>
    internal static object? ForProtocol(ParameterInfo parameter, object? value)
        => IsSecret(parameter) && IsSet(value) ? Mask : value;

    /// <summary>
    /// The configuration of an implementation as the protocol keeps it: only the keys that name a parameter of its
    /// constructor, the secret ones masked. Binding ignores any other key, and a mistyped one could carry a secret.
    /// </summary>
    internal static Parameterization ForProtocol(ConstructorInfo constructor, Parameterization config)
    {
        var recorded = new Parameterization();
        foreach (var parameter in constructor.GetParameters())
        {
            if (parameter.Name is { } name && config.TryGetValue(name, out var value))
                recorded[name] = ForProtocol(parameter, value);
        }

        return recorded;
    }

    /// <summary>
    /// Why the parameter must not be marked secret, or <see langword="null"/> when it may be. Only a text may: the base
    /// configuration carries text anyway, and a text never fails the conversion whose message would name the value.
    /// </summary>
    internal static string? TypeError(ParameterInfo parameter, string subject, string implementation)
        => IsSecret(parameter) && parameter.ParameterType != typeof(string)
            ? $"{subject} <{implementation}>: the parameter '{parameter.Name}' is marked [SecretConfigValue] but takes <{ConstructorBinding.FormatTypeName(parameter.ParameterType)}>. A secret is a text (string)."
            : null;

    // An empty text is no value either: the options binder turns an empty setting into "", and showing it reveals
    // nothing. A value read back from JSON, such as from the snapshot of a delivery reaction, counts the same way.
    private static bool IsSet(object? value) => value switch
    {
        null => false,
        string text => text.Length > 0,
        JsonElement { ValueKind: JsonValueKind.Null } => false,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() is { Length: > 0 },
        _ => true,
    };
}
