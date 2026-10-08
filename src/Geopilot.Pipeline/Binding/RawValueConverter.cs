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

    private delegate bool TryParseString(string value, out object? parsed);

    /// <summary>
    /// The supported string-to-value conversions, shared as the single source of truth: <see cref="TryConvert"/>
    /// looks them up and the load-time binding check consults <see cref="IsStringConvertibleTarget"/>, so both
    /// accept the same string-to-X pairs. Each parser reports whether the string parsed and, if so, the value.
    /// </summary>
    private static readonly Dictionary<Type, TryParseString> StringConversions = new()
    {
        [typeof(int)] = TryParseInt,
        [typeof(double)] = TryParseDouble,
        [typeof(bool)] = TryParseBool,
        [typeof(TimeSpan)] = TryParseTimeSpan,
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

            if (StringConversions.TryGetValue(effectiveTargetType, out var parse) && parse(s, out convertedValue))
                return true;
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
            || StringConversions.ContainsKey(effectiveTargetType);
    }

    /// <summary>
    /// Whether a value of <paramref name="sourceType"/> could convert to <paramref name="targetType"/> at
    /// the leaf, ignoring collection structure, as a tri-state for the load-time binding check:
    /// <see cref="Convertibility.Yes"/> when the value is assignable in either direction or a string
    /// converts via a supported conversion, <see cref="Convertibility.No"/> when the target is an interface
    /// or abstract type the source is not assignable to (the JSON round-trip cannot build it), and
    /// <see cref="Convertibility.Maybe"/> for a concrete target whose JSON round-trip only the run-time
    /// value decides.
    /// </summary>
    internal static Convertibility CanConvert(Type sourceType, Type targetType)
    {
        if (targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType))
            return Convertibility.Yes;

        if (sourceType == typeof(string) && IsStringConvertibleTarget(targetType))
            return Convertibility.Yes;

        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return effectiveTargetType.IsInterface || effectiveTargetType.IsAbstract
            ? Convertibility.No
            : Convertibility.Maybe;
    }

    /// <summary>
    /// The outcome of <see cref="CanConvert"/>: whether a value of one type could bind to a target for at
    /// least some run-time value.
    /// </summary>
    internal enum Convertibility
    {
        /// <summary>The target is an interface or abstract type the source is not assignable to; it cannot be built.</summary>
        No,

        /// <summary>A concrete target whose JSON round-trip succeeds or fails depending on the run-time value.</summary>
        Maybe,

        /// <summary>The value is assignable, or a string converts to the target via a supported conversion.</summary>
        Yes,
    }

    private static bool TryParseInt(string value, out object? parsed)
    {
        var success = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);
        parsed = result;
        return success;
    }

    private static bool TryParseDouble(string value, out object? parsed)
    {
        var success = double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result);
        parsed = result;
        return success;
    }

    private static bool TryParseBool(string value, out object? parsed)
    {
        var success = bool.TryParse(value, out var result);
        parsed = result;
        return success;
    }

    private static bool TryParseTimeSpan(string value, out object? parsed)
    {
        var success = TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result);
        parsed = result;
        return success;
    }
}
