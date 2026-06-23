using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.XtfValidatorErrorTree;

/// <summary>
/// Classifies an XTF validator message into a human readable error category.
/// </summary>
/// <remarks>
/// The validator emits no machine readable error code, so categories are matched by regex against the
/// static parts of the message templates. Those templates are the single source of truth in the iox-ili
/// resource bundle <c>ch/interlis/iox_j/validator/ValidatorMessages.properties</c>. When a template changes
/// or is added there, mirror it in <see cref="Patterns"/>.
/// </remarks>
internal static class ErrorTypeClassifier
{
    /// <summary>
    /// Ordered category patterns, first match wins. The specific type, value and constraint patterns are
    /// listed before the generic <c>Attribute {0} ...</c> shapes so that messages such as
    /// "Attribute X has wrong number of values" are not swallowed by a broader rule.
    /// </summary>
    private static readonly (Regex Pattern, string Category)[] Patterns =
    {
        (new Regex(@"^Mandatory Constraint .* is not true\.$"), "Mandatory constraint not true"),
        (new Regex(@"^Plausibility Constraint .* is not true\.$"), "Plausibility constraint not true"),
        (new Regex(@"^Set Constraint .* is not true\.$"), "Set constraint not true"),
        (new Regex(@"^Unique constraint .* is violated!"), "Unique constraint violated"),
        (new Regex(@"^Existence constraint .* is violated!"), "Existence constraint violated"),

        (new Regex(@"is not a number in attribute "), "Value is not a number"),
        (new Regex(@"is out of range in attribute "), "Numeric value out of range"),
        (new Regex(@"is not a member of the enumeration in attribute "), "Value not a member of enumeration"),
        (new Regex(@"is not a BOOLEAN in attribute "), "Value not a BOOLEAN"),
        (new Regex(@"is not a valid UUID in attribute "), "Value not a valid UUID"),
        (new Regex(@"is not a valid OID in attribute "), "Value not a valid OID"),
        (new Regex(@"is not a valid Date in attribute "), "Value not a valid date"),
        (new Regex(@"is a keyword in attribute "), "Value is a reserved keyword"),

        (new Regex(@"^invalid format of date value <.*> in attribute "), "Invalid date format"),
        (new Regex(@"^invalid format of time value <.*> in attribute "), "Invalid time format"),
        (new Regex(@"^invalid format of datetime value <.*> in attribute "), "Invalid datetime format"),
        (new Regex(@"^date value <.*> is not in range in attribute "), "Date value out of range"),
        (new Regex(@"^time value <.*> is not in range in attribute "), "Time value out of range"),
        (new Regex(@"^datetime value <.*> is not in range in attribute "), "Datetime value out of range"),
        (new Regex(@"^invalid format of INTERLIS\.NAME value <.*> in attribute "), "Invalid INTERLIS.NAME format"),
        (new Regex(@"^invalid format of INTERLIS\.URI value <.*> in attribute "), "Invalid INTERLIS.URI format"),

        (new Regex(@"is not in range in attribute "), "Value not in range"),

        (new Regex(@"^The value <.*> is not a Polyline in attribute "), "Value is not a polyline"),
        (new Regex(@"^The value <.*> is not a Polygon in attribute "), "Value is not a polygon"),
        (new Regex(@"^The value <.*> is not a Coord in attribute "), "Value is not a coordinate"),

        (new Regex(@"does not satisfy the domain constraint "), "Domain constraint not satisfied"),
        (new Regex(@"^Attribute <.*> has a invalid value"), "Invalid formatted value"),
        (new Regex(@"^Value <.*> is a out of range in attribute <"), "Formatted value out of range"),

        (new Regex(@"^unknown class <.*> in attribute "), "Unknown class in attribute"),
        (new Regex(@"^Attribute .* requires a value$"), "Mandatory attribute missing"),
        (new Regex(@"^Attribute .* has wrong number of values$"), "Wrong number of values"),
        (new Regex(@"^Attribute .* is length restricted to "), "Text too long"),
        (new Regex(@"must not contain control characters$"), "Control characters in text"),
        (new Regex(@"^Attribute .* requires a (non-abstract )?structure"), "Missing required structure"),
        (new Regex(@"has an unexpected type "), "Unexpected attribute type"),

        (new Regex(@"^Wrong COORD structure"), "Invalid COORD structure"),
        (new Regex(@"^Not a type of COORD$"), "Invalid COORD structure"),
        (new Regex(@"^Wrong ARC structure"), "Invalid ARC structure"),
        (new Regex(@"^invalid number of segments in POLYLINE$"), "Invalid polyline geometry"),
        (new Regex(@"^invalid number of surfaces"), "Invalid surface geometry"),

        (new Regex(@"^No object found with OID "), "Referenced object not found"),
        (new Regex(@"wrong class .* of target object .* for "), "Wrong target class for reference"),
        (new Regex(@"should associate .* target objects"), "Wrong association multiplicity"),

        (new Regex(@"^unknown property <.*>"), "Unknown property"),
        (new Regex(@"^unknown class <.*>"), "Unknown class"),
    };

    /// <summary>
    /// Classifies the given validator message into an error category.
    /// </summary>
    /// <param name="message">The validator message text.</param>
    /// <returns>The category display name, or <see langword="null"/> when no known pattern matches.</returns>
    public static string? Classify(string message)
    {
        foreach (var (pattern, category) in Patterns)
        {
            if (pattern.IsMatch(message))
                return category;
        }

        return null;
    }
}
