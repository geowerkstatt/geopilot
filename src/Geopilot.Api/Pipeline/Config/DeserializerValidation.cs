using System.ComponentModel.DataAnnotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Custom deserializer that validates the deserialized object.
/// </summary>
public class DeserializerValidation(INodeDeserializer nodeDeserializer) : INodeDeserializer
{
    /// <summary>
    /// Deserializes the YAML input and validates the resulting object.
    /// </summary>
    /// <param name="reader">The YAML parser.</param>
    /// <param name="expectedType">The expected type of the object to deserialize.</param>
    /// <param name="nestedObjectDeserializer">A function to deserialize nested objects.</param>
    /// <param name="value">The deserialized object.</param>
    /// <param name="rootDeserializer">The root deserializer.</param>
    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        if (!nodeDeserializer.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer))
            return false;
        if (value != null)
        {
            var context = new ValidationContext(value);
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(value, context, results, true))
                return true;
            var message = value.GetType().Name + ": " + string.Join("\n", results.Select(r => r.ErrorMessage));
            throw new YamlException(message);
        }
        else
        {
            return true;
        }
    }
}
