using System.Xml;
using System.Xml.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Provides a method to parse XTF log files.
/// </summary>
internal static class XtfLogParser
{
    /// <summary>
    /// Parses an XTF log file.
    /// </summary>
    /// <param name="xtfLogReader">Reader of an XTF log file.</param>
    /// <returns>The entries of the log basket.</returns>
    public static List<LogError> Parse(TextReader xtfLogReader)
    {
        using var xmlReader = XmlReader.Create(xtfLogReader);
        var serializer = new XmlSerializer(typeof(Transfer));
        var transfer = serializer.Deserialize(xmlReader) as Transfer;
        if (transfer != null)
        {
            if (transfer?.Datasection?.ErrorLogBasket?.Errors != null)
                return transfer.Datasection.ErrorLogBasket.Errors;
            else
                return new List<LogError>();
        }
        else
        {
            throw new InvalidOperationException("Failed to parse XTF log file: Deserialized object is null.");
        }
    }
}
