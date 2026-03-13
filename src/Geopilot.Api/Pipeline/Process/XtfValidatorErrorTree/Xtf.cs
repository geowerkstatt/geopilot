using System.Xml.Serialization;

namespace Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

/// <summary>
/// Represents the root element of an INTERLIS 2.3 transfer file, containing the data section and associated metadata.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
[XmlRoot(ElementName = "TRANSFER", Namespace = "http://www.interlis.ch/INTERLIS2.3", IsNullable = false)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Accepted for xtf log XML classes.")]
public class Transfer
{
    /// <summary>
    /// Gets or sets the data section represented by the <see cref="Datasection"/> object.
    /// </summary>
    [XmlElement("DATASECTION")]
    public Datasection? Datasection { get; set; }
}

/// <summary>
/// Represents a data section within an INTERLIS 2.3 XML document, containing optional error log information.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
public class Datasection
{
    /// <summary>
    /// Gets or sets the error log basket containing validation errors for the current operation.
    /// </summary>
    [XmlElement("IliVErrors.ErrorLog")]
    public ErrorLogBasket? ErrorLogBasket { get; set; }
}

/// <summary>
/// Represents a container for error log entries associated with a specific basket in an INTERLIS 2.3 data exchange.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
public class ErrorLogBasket
{
    /// <summary>
    /// Gets or sets the unique identifier for the bid associated with this entity.
    /// </summary>
    [XmlAttribute("BID")]
    public string? Bid { get; set; }

    /// <summary>
    /// Gets or sets the collection of error log entries associated with the current operation.
    /// </summary>
    [XmlElement("IliVErrors.ErrorLog.Error")]
    public List<LogError>? Errors { get; set; }
}

/// <summary>
/// Represents an error entry in a log, including details such as the error message, type, location, and related object
/// information.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
public class LogError
{
    /// <summary>
    /// Gets or sets the transaction identifier associated with this instance.
    /// </summary>
    [XmlAttribute("TID")]
    public string? Tid { get; set; }

    /// <summary>
    /// Gets or sets the message text associated with this instance.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the type associated with the current instance.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets an optional tag associated with the object.
    /// </summary>
    public string? ObjTag { get; set; }

    /// <summary>
    /// Gets or sets the name or connection string of the data source to be used.
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Gets or sets the line number associated with the current item, if available.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Gets or sets the geometric shape associated with this instance.
    /// </summary>
    public Geometry? Geometry { get; set; }

    /// <summary>
    /// Gets or sets the technical details associated with the entity.
    /// </summary>
    public string? TechDetails { get; set; }
}

/// <summary>
/// Represents a geometric element containing coordinate information as defined by the INTERLIS 2.3 schema.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
public class Geometry
{
    /// <summary>
    /// Gets or sets the coordinate information associated with this entity.
    /// </summary>
    [XmlElement("COORD")]
    public Coord? Coord { get; set; }
}

/// <summary>
/// Represents a two-dimensional coordinate with decimal values for each axis.
/// </summary>
[Serializable]
[XmlType(AnonymousType = true, Namespace = "http://www.interlis.ch/INTERLIS2.3")]
public class Coord
{
    /// <summary>
    /// Gets or sets the value of C1.
    /// </summary>
    public decimal C1 { get; set; }

    /// <summary>
    /// Gets or sets the value of the C2 property.
    /// </summary>
    public decimal C2 { get; set; }
}
