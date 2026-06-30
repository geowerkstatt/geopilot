using Geopilot.PipelineCore.Pipeline;
using System.Text.RegularExpressions;

namespace Geopilot.Pipeline.Processes.XtfErrorVisualization;

/// <summary>
/// Classifies an XTF validator message into a human readable, multilingual error category.
/// </summary>
/// <remarks>
/// The validator emits no machine readable error code, so categories are matched by regex against the
/// static parts of the message templates. Those templates are the single source of truth in the iox-ili
/// resource bundle <c>ch/interlis/iox_j/validator/ValidatorMessages.properties</c>. When a template changes
/// or is added there, mirror it in <see cref="Patterns"/>. The category text is the only part we localize;
/// the raw validator message stays in the language the validator produced.
/// </remarks>
internal static class ErrorTypeClassifier
{
    /// <summary>
    /// Builds a category label for the four supported languages.
    /// </summary>
    private static LocalizedText Category(string de, string fr, string it, string en) =>
        new Dictionary<string, string> { ["de"] = de, ["fr"] = fr, ["it"] = it, ["en"] = en };

    /// <summary>
    /// Ordered category patterns, first match wins. The specific type, value and constraint patterns are
    /// listed before the generic <c>Attribute {0} ...</c> shapes so that messages such as
    /// "Attribute X has wrong number of values" are not swallowed by a broader rule.
    /// </summary>
    private static readonly (Regex Pattern, LocalizedText Category)[] Patterns =
    {
        (new Regex(@"^Mandatory Constraint .* is not true\.$"), Category("Mandatory Constraint nicht erfüllt", "Contrainte Mandatory non remplie", "Vincolo Mandatory non soddisfatto", "Mandatory constraint not true")),
        (new Regex(@"^Plausibility Constraint .* is not true\.$"), Category("Plausibility Constraint nicht erfüllt", "Contrainte Plausibility non remplie", "Vincolo Plausibility non soddisfatto", "Plausibility constraint not true")),
        (new Regex(@"^Set Constraint .* is not true\.$"), Category("Set Constraint nicht erfüllt", "Contrainte Set non remplie", "Vincolo Set non soddisfatto", "Set constraint not true")),
        (new Regex(@"^Unique constraint .* is violated!"), Category("Unique Constraint verletzt", "Contrainte Unique violée", "Vincolo Unique violato", "Unique constraint violated")),
        (new Regex(@"^Existence constraint .* is violated!"), Category("Existence Constraint verletzt", "Contrainte Existence violée", "Vincolo Existence violato", "Existence constraint violated")),

        (new Regex(@"is not a number in attribute "), Category("Wert ist keine Zahl", "La valeur n'est pas un nombre", "Il valore non è un numero", "Value is not a number")),
        (new Regex(@"is out of range in attribute "), Category("Numerischer Wert ausserhalb des Bereichs", "Valeur numérique hors plage", "Valore numerico fuori intervallo", "Numeric value out of range")),
        (new Regex(@"is not a member of the enumeration in attribute "), Category("Wert nicht in der Aufzählung enthalten", "Valeur absente de l'énumération", "Valore non presente nell'enumerazione", "Value not a member of enumeration")),
        (new Regex(@"is not a BOOLEAN in attribute "), Category("Wert ist kein BOOLEAN", "La valeur n'est pas un BOOLEAN", "Il valore non è un BOOLEAN", "Value not a BOOLEAN")),
        (new Regex(@"is not a valid UUID in attribute "), Category("Ungültige UUID", "UUID non valide", "UUID non valido", "Value not a valid UUID")),
        (new Regex(@"is not a valid OID in attribute "), Category("Ungültige OID", "OID non valide", "OID non valido", "Value not a valid OID")),
        (new Regex(@"is not a valid Date in attribute "), Category("Ungültiges Datum", "Date non valide", "Data non valida", "Value not a valid date")),
        (new Regex(@"is a keyword in attribute "), Category("Wert ist ein reserviertes Schlüsselwort", "La valeur est un mot-clé réservé", "Il valore è una parola chiave riservata", "Value is a reserved keyword")),

        (new Regex(@"^invalid format of date value <.*> in attribute "), Category("Ungültiges Datumsformat", "Format de date non valide", "Formato data non valido", "Invalid date format")),
        (new Regex(@"^invalid format of time value <.*> in attribute "), Category("Ungültiges Zeitformat", "Format d'heure non valide", "Formato ora non valido", "Invalid time format")),
        (new Regex(@"^invalid format of datetime value <.*> in attribute "), Category("Ungültiges Datum-/Zeitformat", "Format date-heure non valide", "Formato data-ora non valido", "Invalid datetime format")),
        (new Regex(@"^date value <.*> is not in range in attribute "), Category("Datumswert ausserhalb des Bereichs", "Valeur de date hors plage", "Valore di data fuori intervallo", "Date value out of range")),
        (new Regex(@"^time value <.*> is not in range in attribute "), Category("Zeitwert ausserhalb des Bereichs", "Valeur d'heure hors plage", "Valore di ora fuori intervallo", "Time value out of range")),
        (new Regex(@"^datetime value <.*> is not in range in attribute "), Category("Datum-/Zeitwert ausserhalb des Bereichs", "Valeur date-heure hors plage", "Valore data-ora fuori intervallo", "Datetime value out of range")),
        (new Regex(@"^invalid format of INTERLIS\.NAME value <.*> in attribute "), Category("Ungültiges INTERLIS.NAME-Format", "Format INTERLIS.NAME non valide", "Formato INTERLIS.NAME non valido", "Invalid INTERLIS.NAME format")),
        (new Regex(@"^invalid format of INTERLIS\.URI value <.*> in attribute "), Category("Ungültiges INTERLIS.URI-Format", "Format INTERLIS.URI non valide", "Formato INTERLIS.URI non valido", "Invalid INTERLIS.URI format")),

        (new Regex(@"is not in range in attribute "), Category("Wert ausserhalb des Bereichs", "Valeur hors plage", "Valore fuori intervallo", "Value not in range")),

        (new Regex(@"^The value <.*> is not a Polyline in attribute "), Category("Wert ist keine Polylinie", "La valeur n'est pas une polyligne", "Il valore non è una polilinea", "Value is not a polyline")),
        (new Regex(@"^The value <.*> is not a Polygon in attribute "), Category("Wert ist kein Polygon", "La valeur n'est pas un polygone", "Il valore non è un poligono", "Value is not a polygon")),
        (new Regex(@"^The value <.*> is not a Coord in attribute "), Category("Wert ist keine Koordinate", "La valeur n'est pas une coordonnée", "Il valore non è una coordinata", "Value is not a coordinate")),

        (new Regex(@"does not satisfy the domain constraint "), Category("Wertebereichsbedingung nicht erfüllt", "Contrainte de domaine non satisfaite", "Vincolo di dominio non soddisfatto", "Domain constraint not satisfied")),
        (new Regex(@"^Attribute <.*> has a invalid value"), Category("Ungültiger formatierter Wert", "Valeur formatée non valide", "Valore formattato non valido", "Invalid formatted value")),
        (new Regex(@"^Value <.*> is a out of range in attribute <"), Category("Formatierter Wert ausserhalb des Bereichs", "Valeur formatée hors plage", "Valore formattato fuori intervallo", "Formatted value out of range")),

        (new Regex(@"^unknown class <.*> in attribute "), Category("Unbekannte Klasse im Attribut", "Classe inconnue dans l'attribut", "Classe sconosciuta nell'attributo", "Unknown class in attribute")),
        (new Regex(@"^Attribute .* requires a value$"), Category("Pflichtattribut fehlt", "Attribut obligatoire manquant", "Attributo obbligatorio mancante", "Mandatory attribute missing")),
        (new Regex(@"^Attribute .* has wrong number of values$"), Category("Falsche Anzahl Werte", "Nombre de valeurs incorrect", "Numero di valori errato", "Wrong number of values")),
        (new Regex(@"^Attribute .* is length restricted to "), Category("Text zu lang", "Texte trop long", "Testo troppo lungo", "Text too long")),
        (new Regex(@"must not contain control characters$"), Category("Steuerzeichen im Text", "Caractères de contrôle dans le texte", "Caratteri di controllo nel testo", "Control characters in text")),
        (new Regex(@"^Attribute .* requires a (non-abstract )?structure"), Category("Erforderliche Struktur fehlt", "Structure requise manquante", "Struttura richiesta mancante", "Missing required structure")),
        (new Regex(@"has an unexpected type "), Category("Unerwarteter Attributtyp", "Type d'attribut inattendu", "Tipo di attributo inatteso", "Unexpected attribute type")),

        (new Regex(@"^Wrong COORD structure"), Category("Ungültige COORD-Struktur", "Structure COORD non valide", "Struttura COORD non valida", "Invalid COORD structure")),
        (new Regex(@"^Not a type of COORD$"), Category("Ungültige COORD-Struktur", "Structure COORD non valide", "Struttura COORD non valida", "Invalid COORD structure")),
        (new Regex(@"^Wrong ARC structure"), Category("Ungültige ARC-Struktur", "Structure ARC non valide", "Struttura ARC non valida", "Invalid ARC structure")),
        (new Regex(@"^invalid number of segments in POLYLINE$"), Category("Ungültige Polyliniengeometrie", "Géométrie de polyligne non valide", "Geometria polilinea non valida", "Invalid polyline geometry")),
        (new Regex(@"^invalid number of surfaces"), Category("Ungültige Flächengeometrie", "Géométrie de surface non valide", "Geometria di superficie non valida", "Invalid surface geometry")),

        (new Regex(@"^No object found with OID "), Category("Referenziertes Objekt nicht gefunden", "Objet référencé introuvable", "Oggetto referenziato non trovato", "Referenced object not found")),
        (new Regex(@"wrong class .* of target object .* for "), Category("Falsche Zielklasse für Referenz", "Classe cible incorrecte pour la référence", "Classe di destinazione errata per il riferimento", "Wrong target class for reference")),
        (new Regex(@"should associate .* target objects"), Category("Falsche Beziehungskardinalität", "Cardinalité d'association incorrecte", "Cardinalità di associazione errata", "Wrong association multiplicity")),

        (new Regex(@"^unknown property <.*>"), Category("Unbekannte Eigenschaft", "Propriété inconnue", "Proprietà sconosciuta", "Unknown property")),
        (new Regex(@"^unknown class <.*>"), Category("Unbekannte Klasse", "Classe inconnue", "Classe sconosciuta", "Unknown class")),
    };

    /// <summary>
    /// Classifies the given validator message into a multilingual error category.
    /// </summary>
    /// <param name="message">The validator message text.</param>
    /// <returns>The category label, or <see langword="null"/> when no known pattern matches.</returns>
    public static LocalizedText? Classify(string message)
    {
        foreach (var (pattern, category) in Patterns)
        {
            if (pattern.IsMatch(message))
                return category;
        }

        return null;
    }
}
