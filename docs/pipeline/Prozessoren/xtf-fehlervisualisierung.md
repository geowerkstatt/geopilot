# XTF Fehlervisualisierung

Wandelt das XTF-Log der [XTF Validierung](xtf-validierung.md) in eine zusammengesetzte Fehlervisualisierung um, die eine Kartenansicht und eine Error-Tree-Ansicht derselben Fehler kombiniert. Über `include` wird gewählt, welche Ansichten erzeugt werden (Standard: beide). Beide Ansichten teilen sich eine gemeinsame Hülle (Envelope), damit das Frontend sie zusammen in einer Komponente darstellen und untereinander verknüpfen kann.

## Implementierung

Ein XTF Fehlervisualisierungs-Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfErrorVisualization.XtfErrorVisualizationProcess` definieren.

## Konfiguration

- `include`: Optionaler Parameter vom Typ `HashSet<string>`. Wählt die zu erzeugenden Ansichten: `map` (Kartenansicht) und/oder `tree` (Error Tree). Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, werden beide Ansichten erzeugt.
- `baseMapWmtsCapabilitiesUrl`: Optionaler Parameter vom Typ `string`. Übersteuert die WMTS-Capabilities-URL der Hintergrundkarte für die Kartenansicht. Wenn `null` oder leer, wird die eingebaute Standard-Hintergrundkarte verwendet.
- `baseMapAttribution`: Optionaler Parameter vom Typ `string`. Übersteuert den Copyright-/Urhebervermerk der Hintergrundkarte (Daten-Owner, z.B. `swisstopo`), symmetrisch zu `baseMapWmtsCapabilitiesUrl`. Wenn `null` oder leer, wird der eingebaute Standardwert verwendet. Das Frontend zeigt den Vermerk mit einem lokalisierten "©"-Präfix in der linken unteren Ecke der Karte an.
- `baseMapAttributionUrl`: Optionaler Parameter vom Typ `string`. Übersteuert die URL, auf die der Copyright-Vermerk verlinkt (z.B. die Nutzungsbedingungen des Karten-Owners). Wenn `null` oder leer, wird der eingebaute Standardwert verwendet. Da `null` und leer auf den Standardwert zurückfallen, ist immer eine URL wirksam und der Vermerk wird stets als Link dargestellt. Wer `baseMapAttribution` übersteuert, sollte deshalb auch `baseMapAttributionUrl` passend setzen, sonst verlinkt der angepasste Vermerk weiterhin auf den Standardwert (z.B. swisstopo).
- `groupBy`: Optionaler Parameter vom Typ `IReadOnlyList<string>`. Metadaten-Schlüssel, nach denen das Frontend den Error Tree gruppiert, äusserste Ebene zuerst (z.B. `["Model", "Topic", "Class"]`). Wenn `null` oder leer, wird nicht gruppiert (flache Liste). Ein Schlüssel, der auf einem Fehler nicht vorhanden ist, landet in einer separaten Gruppe "Ohne Zuordnung".
- `filterBy`: Optionaler Parameter vom Typ `IReadOnlyList<string>`. Metadaten-Schlüssel, die das Frontend als Filter anbietet, in Anzeigereihenfolge (z.B. `["Model", "Topic", "Class", "Error type"]`). Der Filter wirkt auf Karte und Baum gleichzeitig. Wenn `null` oder leer, werden keine Filter angeboten.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `xtfLog`: Ein Input-File vom Typ `IPipelineFile`. Auf diesen Namen muss genau ein File gemappt werden, aus dem die Fehler gelesen werden. Es ist das XTF-Log der Validierung (Output `XtfLog` der [XTF Validierung](xtf-validierung.md)).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Visualization`: Die zusammengesetzte Fehlervisualisierung (Kartenansicht und/oder Error Tree) in einem gemeinsamen Envelope. Wird mit der Output-Aktion `Visualization` versehen, damit die Visualisierung im Frontend dargestellt wird.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche meldet, dass die Fehlervisualisierung erstellt wurde.

## Format des `Visualization`-Outputs

Der `Visualization`-Output ist ein Envelope mit dem Diskriminator `xtfError` und einer Nutzlast aus optionaler Karten- und Baum-Ansicht (je nach `include`). Der Baum wird als **flache Liste** von Fehlern geliefert; das Frontend baut daraus die dargestellte Hierarchie, indem es die Fehler nach den `groupBy`-Schlüsseln gruppiert. Serialisiert wird als camelCase-JSON, `null`-Felder werden weggelassen:

```json
{
  "type": "xtfError",
  "data": {
    "map": {
      "layers": [
        {
          "title": { "de": "Hintergrundkarte", "en": "Base map", "fr": "Fond de carte", "it": "Mappa di base" },
          "wmts": "https://example.com/wmts/1.0.0/WMTSCapabilities.xml",
          "layerIds": ["ch.swisstopo.pixelkarte-farbe"],
          "attribution": "swisstopo",
          "attributionUrl": "https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste"
        },
        {
          "color": "#e53835",
          "features": [
            { "errorId": "e0", "geom": "POINT(2600000 1200000)", "info": "Fehlerbeschreibung" }
          ]
        }
      ]
    },
    "tree": {
      "items": [
        {
          "id": "e0",
          "label": "obj123",
          "severity": "error",
          "metadata": {
            "Error type": {
              "de": "Pflichtattribut fehlt",
              "en": "Mandatory attribute missing",
              "fr": "Attribut obligatoire manquant",
              "it": "Attributo obbligatorio mancante"
            },
            "Model": "Schutzbauten_V1_1",
            "Topic": "Einzelobjekte",
            "Class": "Gebaeudeeingang",
            "Message": "Attribute IstExaktDefiniert requires a value",
            "Line": "42"
          }
        }
      ],
      "groupBy": ["Model", "Topic", "Class"]
    },
    "filterBy": ["Model", "Topic", "Class", "Error type"]
  }
}
```

- `data.map` und `data.tree` erscheinen nur, wenn die jeweilige Ansicht via `include` erzeugt wurde. `data.filterBy` erscheint nur, wenn ein Baum erzeugt wurde.
- `map.layers` werden in Reihenfolge gezeichnet. Ein Layer ist entweder ein WMTS-Layer (`wmts` = Capabilities-URL, optional `layerIds`) oder ein Feature-Layer (`features`, optional `color` als Hex-Farbe). `title` (lokalisiert) ist optional. Ein Layer kann zudem einen Copyright-Vermerk tragen: `attribution` (Daten-Owner bzw. Anzeigetext) und optional `attributionUrl` (Link-Ziel); das Frontend zeigt ihn mit lokalisiertem "©"-Präfix unten links an.
- Pro `features`-Eintrag: `geom` als WKT (z.B. `POINT(...)`), `errorId` als stabile Fehler-ID, `info` als Anzeigetext.
- `tree.items` ist eine flache Liste; jedes Element ist ein Fehler und wird zum Blatt des Baums. Pro Element: `id` (stabile Fehler-ID, optional), `label` (Anzeigetext des Blattes), `severity` (`error` oder `warning`, daraus leitet das Frontend Icon und Farbe ab) und `metadata` (Key/Value). Ein Metadaten-Wert ist entweder ein einfacher String (Daten, z.B. ein Klassenname) oder ein `LocalizedText` (eine generierte Beschriftung wie die Fehlerkategorie, serialisiert als Objekt je Sprache).
- `tree.groupBy` sind die Metadaten-Schlüssel, nach denen das Frontend die Items zur Hierarchie gruppiert (äusserste Ebene zuerst); eine leere Liste ergibt eine flache Liste. `data.filterBy` sind die im Frontend als Filter angebotenen Schlüssel; der Filter wirkt auf Karte und Baum.
- `id` des Tree-Items und `errorId` des Map-Features sind identisch; daran verknüpft das Frontend Karte und Baum (Cross-Select).

