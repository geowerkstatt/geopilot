using Geopilot.Pipeline.Config;
using System.Reflection;
using System.Text.Json;

namespace Geopilot.Pipeline;

/// <summary>
/// Binds the configuration of a process or a channel to the single public constructor of its implementation. Shared,
/// so both read their configuration by the same rules. <c>subject</c> names what is bound ("Process" or "Channel")
/// and starts every message.
/// </summary>
internal static class ConstructorBinding
{
    /// <summary>
    /// The single public constructor of <paramref name="type"/>, which the configuration binds to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type does not have exactly one public constructor.</exception>
    internal static ConstructorInfo GetSingleConstructor(Type type, string implementation, string subject)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.Length != 1)
            throw new InvalidOperationException($"{subject} <{implementation}> has {constructors.Length} public constructors. A {subject} must have exactly one public constructor.");

        return constructors[0];
    }

    /// <summary>
    /// Resolves the configured value for a constructor parameter. Returns false when the
    /// parameter is not configured. A configured value that cannot be converted to the
    /// parameter type is a configuration error and throws, even for a nullable parameter:
    /// silently falling back to null would hide typos in the configuration. A
    /// configured null, such as an empty YAML value, counts as not configured for a
    /// parameter declared non-nullable, so the caller refuses it like a missing one.
    /// </summary>
    internal static bool TryGetConfiguredValue(ParameterInfo parameterInfo, Parameterization config, string subject, out object? convertedValue)
    {
        convertedValue = null;
        if (string.IsNullOrEmpty(parameterInfo.Name) || !config.TryGetValue(parameterInfo.Name, out var rawValue))
            return false;

        // Code without nullable annotations declares nothing, so its reference-type parameters keep taking null.
        if (rawValue is null && new NullabilityInfoContext().Create(parameterInfo).WriteState is NullabilityState.NotNull)
            return false;

        if (RawValueConverter.TryConvert(rawValue, parameterInfo.ParameterType, out convertedValue))
            return true;

        if (rawValue is not null)
            throw new InvalidOperationException($"{subject} initialization: The configured value <{FormatRawValue(rawValue)}> for parameter <{parameterInfo.Name}> cannot be converted to type <{FormatTypeName(parameterInfo.ParameterType)}>.");

        return false;
    }

    /// <summary>
    /// Whether the parameter may be left without a value, which binds it to null.
    /// </summary>
    internal static bool IsNullable(ParameterInfo parameterInfo)
        => new NullabilityInfoContext().Create(parameterInfo).WriteState is NullabilityState.Nullable;

    internal static string FormatRawValue(object rawValue)
    {
        try
        {
            return JsonSerializer.Serialize(rawValue);
        }
        catch (Exception)
        {
            return rawValue.ToString() ?? string.Empty;
        }
    }

    private static string FormatTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
            return FormatTypeName(underlyingType) + "?";

        if (!type.IsGenericType)
            return type.Name;

        var genericName = type.Name[..type.Name.IndexOf('`')];
        return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(FormatTypeName))}>";
    }
}
