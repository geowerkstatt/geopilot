using System.Globalization;
using System.Text.Json;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents a set of parameters for pipeline processing.
/// Supports scalar values, lists and nested objects.
/// </summary>
public class Parameterization : Dictionary<string, object?>
{
    /// <summary>
    /// Initializes a new instance of the Parameterization class.
    /// </summary>
    public Parameterization()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Parameterization class by copying the values from the specified source
    /// instance.
    /// </summary>
    /// <remarks>This constructor creates a deep copy of the provided source instance, ensuring that the new
    /// instance has the same state as the original.</remarks>
    /// <param name="src">The source Parameterization instance from which to copy values. This parameter cannot be null.</param>
    public Parameterization(Parameterization src)
        : base(src)
    {
    }

    /// <summary>
    /// Attempts to convert the specified raw value to the specified target type.
    /// </summary>
    /// <param name="rawValue">The raw value to convert.</param>
    /// <param name="targetType">The target type to convert to.</param>
    /// <param name="convertedValue">The converted value, if the conversion is successful; otherwise, null.</param>
    /// <returns>True if the conversion is successful; otherwise, false.</returns>
    public static bool TryConvertObject(object? rawValue, Type targetType, out object? convertedValue)
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

            if (effectiveTargetType == typeof(int) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                convertedValue = i;
                return true;
            }

            if (effectiveTargetType == typeof(double) && double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
            {
                convertedValue = d;
                return true;
            }

            if (effectiveTargetType == typeof(bool) && bool.TryParse(s, out var b))
            {
                convertedValue = b;
                return true;
            }

            if (effectiveTargetType == typeof(TimeSpan) && TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
            {
                convertedValue = ts;
                return true;
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(rawValue);
            convertedValue = JsonSerializer.Deserialize(json, effectiveTargetType);
            return convertedValue != null;
        }
        catch
        {
            convertedValue = null;
            return false;
        }
    }
}
