using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Geopilot.Pipeline.Processes.XtfMetadata;

/// <summary>
/// A configured metadata path, checked and prepared once: which objects of a transfer file it applies to, and how it
/// reads values from one of them.
/// </summary>
internal sealed partial class XtfObjectPath
{
    /// <summary>
    /// The namespace of the INTERLIS 2.4 transfer format, bound to the prefix <c>ili</c>.
    /// </summary>
    internal const string Interlis24Namespace = "http://www.interlis.ch/xtf/2.4/INTERLIS";

    private const string ModelNamespaceBase = "http://www.interlis.ch/xtf/2.4/";

    private readonly XPathExpression expression;

    private XtfObjectPath(XName objectName, XPathExpression expression)
    {
        ObjectName = objectName;
        this.expression = expression;
    }

    /// <summary>
    /// The element name of the objects the path applies to, taken from its first step.
    /// </summary>
    public XName ObjectName { get; }

    /// <summary>
    /// Checks and prepares a configured path. A prefix stands for the namespace of the model of the same name,
    /// <c>ili</c> for the INTERLIS namespace.
    /// </summary>
    /// <exception cref="InvalidOperationException">The path is empty, does not start with <c>Model:Class</c>, is not a
    /// valid XPath, or computes a value instead of selecting elements or attributes.</exception>
    public static XtfObjectPath Compile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("The metadata path is empty. Configure the path of the value to read.");

        var firstStep = FirstStepPattern().Match(path);
        if (!firstStep.Success)
            throw new InvalidOperationException($"The metadata path <{path}> must start with the class of the objects it reads, written as Model:Class.");

        var namespaces = new XmlNamespaceManager(new NameTable());
        var prefixes = PrefixPattern().Matches(path)
            .Select(match => match.Groups["prefix"].Value)
            .Where(prefix => prefix is not ("xml" or "xmlns"))
            .Distinct(StringComparer.Ordinal);
        foreach (var prefix in prefixes)
            namespaces.AddNamespace(prefix, NamespaceOf(prefix));

        XPathExpression expression;
        try
        {
            expression = XPathExpression.Compile(path, namespaces);
        }
        catch (XPathException e)
        {
            // No inner exception: a failed step reports the innermost message, and that one has to name the path.
            throw new InvalidOperationException($"The metadata path <{path}> is not a valid XPath: {e.Message}");
        }

        if (expression.ReturnType != XPathResultType.NodeSet)
            throw new InvalidOperationException($"The metadata path <{path}> must select elements or attributes, not compute a value.");

        var objectName = XName.Get(firstStep.Groups["class"].Value, NamespaceOf(firstStep.Groups["model"].Value));
        return new XtfObjectPath(objectName, expression);
    }

    /// <summary>
    /// Reads the values the path selects from one object, each without surrounding whitespace. An empty value is no
    /// value and is left out.
    /// </summary>
    public IEnumerable<string> ValuesOf(XElement transferObject)
    {
        // The path starts with the class step, so it is evaluated from a parent the object is placed into.
        var parent = new XElement("object", transferObject);
        foreach (XPathNavigator node in parent.CreateNavigator().Select(expression))
        {
            var value = node.Value.Trim();
            if (value.Length > 0)
                yield return value;
        }
    }

    private static string NamespaceOf(string prefix) =>
        prefix == "ili" ? Interlis24Namespace : ModelNamespaceBase + prefix;

    // A class may be written with its topic, as in Bodenbedeckung.Bodenbedeckung, the way the transfer file names it.
    [GeneratedRegex(@"^\s*(?<model>[A-Za-z][A-Za-z0-9_]*):(?<class>[A-Za-z][A-Za-z0-9_.]*)\s*(?:$|[\[/])")]
    private static partial Regex FirstStepPattern();

    // A prefix is a name before a single colon; the double colon of an axis such as child:: is not one.
    [GeneratedRegex(@"(?<![A-Za-z0-9_.\-])(?<prefix>[A-Za-z][A-Za-z0-9_]*):(?!:)")]
    private static partial Regex PrefixPattern();
}
