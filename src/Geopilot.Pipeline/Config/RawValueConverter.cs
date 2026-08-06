using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Converts a raw value, as produced by the YAML deserializer, to a requested target type. Shared
/// by the process configuration binding and the step input binding, which both take loosely typed
/// values from a definition and feed them to typed process members.
/// </summary>
internal static class RawValueConverter
{
    // Enum values arrive from YAML as strings (also inside lists), so the JSON round-trip
    // must accept enum member names; reading is case-insensitive.
    private static readonly JsonSerializerOptions FallbackSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The supported string-to-value conversions, shared as the single source of truth: <see cref="TryConvert"/>
    /// runs them and the load-time binding check consults <see cref="IsStringConvertibleTarget"/>, so both accept
    /// the same string-to-X pairs. Each reports whether the string parsed and, if so, the converted value.
    /// </summary>
    private static readonly IReadOnlyList<(Type TargetType, Func<string, (bool Success, object? Value)> Parse)> StringConversions = new (Type, Func<string, (bool, object?)>)[]
    {
        (typeof(int), value => (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed), parsed)),
        (typeof(double), value => (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed), parsed)),
        (typeof(bool), value => (bool.TryParse(value, out var parsed), parsed)),
        (typeof(TimeSpan), value => (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed), parsed)),
    };

    /// <summary>
    /// Attempts to convert the specified raw value to the specified target type.
    /// </summary>
    /// <param name="rawValue">The raw value to convert.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="convertedValue">The converted value, if the conversion is successful; otherwise, null.</param>
    /// <returns>True if the conversion is successful; otherwise, false.</returns>
    internal static bool TryConvert(object? rawValue, Type targetType, out object? convertedValue)
    {
        if (targetType == null)
        {
            convertedValue = null;
            return false;
        }

        var nullableTargetType = Nullable.GetUnderlyingType(targetType);
        var effectiveTargetType = nullableTargetType ?? targetType;

        if (rawValue is null)
        {
            convertedValue = null;
            return nullableTargetType != null || !targetType.IsValueType;
        }

        if (effectiveTargetType.IsInstanceOfType(rawValue))
        {
            convertedValue = rawValue;
            return true;
        }

        if (rawValue is string s)
        {
            if (effectiveTargetType == typeof(string))
            {
                convertedValue = s;
                return true;
            }

            if (effectiveTargetType.IsEnum && Enum.TryParse(effectiveTargetType, s, true, out var enumValue))
            {
                convertedValue = enumValue;
                return true;
            }

            foreach (var (conversionTarget, parse) in StringConversions)
            {
                if (conversionTarget != effectiveTargetType)
                    continue;

                var (success, value) = parse(s);
                if (success)
                {
                    convertedValue = value;
                    return true;
                }
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(rawValue, FallbackSerializerOptions);
            convertedValue = JsonSerializer.Deserialize(json, effectiveTargetType, FallbackSerializerOptions);
            return convertedValue != null;
        }
        catch
        {
            convertedValue = null;
            return false;
        }
    }

    /// <summary>
    /// Whether a <see cref="string"/> value can convert to <paramref name="targetType"/> via one of the
    /// supported conversions (another string, an enum, or an entry of <see cref="StringConversions"/>). The
    /// load-time binding check uses this so it accepts the same string-to-X pairs the binder does.
    /// </summary>
    internal static bool IsStringConvertibleTarget(Type targetType)
    {
        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return effectiveTargetType == typeof(string)
            || effectiveTargetType.IsEnum
            || StringConversions.Any(conversion => conversion.TargetType == effectiveTargetType);
    }
}
