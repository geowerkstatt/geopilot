using Geopilot.Pipeline.Config;
using System.Diagnostics.CodeAnalysis;
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
    /// Resolves a parameter that names a file the deployment ships. Its configured value is the path of the file
    /// relative to the resources directory, the root a <c>${file(path)}</c> input reference resolves against. The path
    /// is read raw, because the conversion of <see cref="TryGetConfiguredValue"/> would treat it as the value itself.
    /// Returns false when the parameter is not configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not a path, no resources directory is configured or the
    /// file does not exist.</exception>
    /// <exception cref="PipelineRunException">The path leads out of the resources directory.</exception>
    internal static bool TryGetResourcePath(ParameterInfo parameterInfo, Parameterization config, string? resourcesRoot, string subject, [NotNullWhen(true)] out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrEmpty(parameterInfo.Name) || !config.TryGetValue(parameterInfo.Name, out var rawValue) || rawValue is null)
            return false;

        // A value read back from JSON, such as the snapshot of a delivery reaction, holds its text as JsonElement.
        var path = rawValue switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"{subject} initialization: The configured value <{FormatRawValue(rawValue)}> for parameter <{parameterInfo.Name}> is not the path of a file below the resources directory.");

        if (string.IsNullOrWhiteSpace(resourcesRoot))
            throw new InvalidOperationException($"{subject} initialization: Parameter <{parameterInfo.Name}> references file <{path}>, but no resources directory is configured.");

        fullPath = ResourceFileResolver.ResolveFullPath(resourcesRoot, path);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException($"{subject} initialization: Parameter <{parameterInfo.Name}> references file <{path}>, which does not exist below the resources directory.");

        return true;
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
