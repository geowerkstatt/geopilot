# XTF Diff Visualisierung

Zeigt die Änderungen eines Diffs des [XTF Diff](xtf-diff.md) in der Lieferansicht: jede Änderung in einer Tabelle, und die geänderten Objekte auf einer Karte, den alten Stand unter dem neuen. Tabelle und Karte sind verknüpft wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md): Ein Filter nach Text und Änderungsart wirkt auf beide, eine Auswahl in der Tabelle hebt das Objekt auf der Karte hervor und umgekehrt, und zum ausgewählten Objekt erscheinen alle seine Änderungen, den Wert vorher und nachher nebeneinander. Schon das Zeigen mit der Maus auf eine Zeile oder ein Feature hebt das Objekt auf der anderen Seite hervor, und ein Klick auf der Karte zeigt die Klasse des Objekts und seine Änderungen in einem Popup. Im Vollbild liegen Filter, Tabelle und Detail neben der Karte. Orange steht überall für den alten Stand und Blau für den neuen: Jede Zeile zeigt in diesen Farben, in welchen Ständen ihr Objekt auf der Karte liegt.

Die Karte zeigt die Lagen des Diffs (`oldLocation`, `newLocation`) unverändert, der Prozess rechnet keine Geometrie um. Ein Objekt erscheint in den Ebenen "Vorher" und "Nachher" je höchstens einmal, auch wenn es mehrere Änderungen hat: ein gelöschtes nur unter "Vorher", ein hinzugefügtes nur unter "Nachher". Haben seine Änderungen verschiedene Lagen, etwa eine geänderte Geometrie in einer Struktur neben der Geometrie des Objekts, fasst die Karte sie zu einer `GEOMETRYCOLLECTION` zusammen, so wie das Tool ein Objekt mit mehreren Geometrie-Attributen schreibt. Eine Änderung ohne Lage, etwa an einem Objekt ohne eigene Geometrie oder aus einem Diff ohne Lagen, steht nur in der Tabelle. Hat keine Änderung eine Lage, entfällt die Karte. Ohne Änderungen zeigt die Visualisierung einen Hinweis.

## Implementierung

Eine XTF Diff Visualisierung muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfDiffVisualization.XtfDiffVisualizationProcess` definieren.

## Konfiguration

- `include`: Optionaler Parameter vom Typ `IReadOnlyList<DiffView>`, die gezeigten Ansichten wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md): `map` (die Karte mit dem Popup) und `table` (die Tabelle mit dem Filter und dem Detail des ausgewählten Objekts). Die Gross-/Kleinschreibung ist unerheblich, ein anderer Wert lässt die Validierung beim Applikationsstart scheitern. Wenn `null` oder leer, werden beide gezeigt. Ohne Tabelle verhält sich die Karte wie die Karte der Fehlervisualisierung ohne Baum: Das Popup nennt nur die Klasse der Objekte, und es gibt keinen Filter. Ist nur die Karte gewählt und hat keine Änderung eine Lage, zeigt die Visualisierung einen Hinweis.
- `baseMapWmtsCapabilitiesUrl`, `baseMapAttribution`, `baseMapAttributionUrl`: Optional, die Hintergrundkarte, gleich wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md).

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `diff`: Das Diff vom Typ `IPipelineFile`, genau eine Datei im JSON-Format des XTF-Diff-Tools ([OutputFileDescription.md](https://github.com/geowerkstatt/XTF-Diff-Tool/blob/main/OutputFileDescription.md)), in der Regel der Output `Diff` des [XTF Diff](xtf-diff.md).

Lassen sich die Stände nicht vergleichen, ist `Diff` leer, und der Schritt scheitert, weil die Datei fehlt. Bricht die Pipeline in diesem Fall nicht schon beim Vergleich ab, braucht der Schritt eine `skip_conditions` wie im Beispiel. Sie greift auch, wenn der Vergleich selbst übersprungen wurde. Entspricht die Datei nicht dem Format des Tools, scheitert der Schritt mit einer Meldung, die den Grund nennt, etwa die Nummer der Änderung ohne `oid`.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Visualization`: Die Visualisierung in einem Envelope mit dem Diskriminator `xtfDiff`. Wird mit der Output-Aktion `Visualization` versehen, damit die Visualisierung im Frontend dargestellt wird.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche meldet, dass die Visualisierung erstellt wurde.

## Beispiel

Ein Schritt nach dem Vergleich `comparison` (siehe Beispiel beim [XTF Diff](xtf-diff.md)), der ohne Diff übersprungen wird:

```yaml
processes:
  - id: xtf_diff_visualizer
    implementation: Geopilot.Pipeline.Processes.XtfDiffVisualization.XtfDiffVisualizationProcess

pipelines:
  - id: xtf_diff
    steps:
      # ... XTF Matcher, Alt/Neu Matcher und Vergleich wie beim XTF Diff
      - id: diff_visualization
        display_name:
          de: Differenzvisualisierung
          en: Diff Visualization
          fr: Visualisation des différences
          it: Visualizzazione delle differenze
        process_id: xtf_diff_visualizer
        conditions:
          pre:
            skip_conditions:
              - id: no-diff
                expression: "Length([comparison.Diff]) == 0"
                message:
                  de: "Ohne Vergleich gibt es keine Differenzen anzuzeigen."
                  en: "Without a comparison there are no differences to show."
                  fr: "Sans comparaison, il n'y a pas de différences à afficher."
                  it: "Senza confronto non ci sono differenze da mostrare."
        input:
          diff: "${step_output(comparison.Diff)}"
        output_actions:
          - property: Visualization
            actions:
              - Visualization
          - property: StatusMessage
            actions:
              - StatusMessage
```

## Format des `Visualization`-Outputs

Der `Visualization`-Output ist ein Envelope mit dem Diskriminator `xtfDiff`. Wie bei der Fehlervisualisierung enthält die Nutzlast nur die Ansichten, die erscheinen: die Tabelle (`table`) mit jeder Änderung des Diffs in seiner Reihenfolge (`changes`), sofern `include` sie enthält, und die Karte (`map`), sofern `include` sie enthält und eine Änderung eine Lage hat. Die Karte besteht aus der Hintergrundkarte und den Ebenen "Vorher" und "Nachher" mit einem Feature pro Objekt, dessen Id die OID des Objekts ist und das mehrere Lagen als `GEOMETRYCOLLECTION` trägt; über diese Id verweisen die Änderungen auf ihre Features. `style` sagt dem Frontend, wie es eine Ebene zeichnet: `old` und `new` in eigenen Farben, ohne `style` in der Standardfarbe der Karte. Listen und Strukturen stehen in `oldValue` und `newValue` als kompaktes JSON. Serialisiert wird als camelCase-JSON, `null`-Felder werden weggelassen:

```json
{
  "type": "xtfDiff",
  "data": {
    "map": {
      "layers": [
        {
          "title": { "de": "Hintergrundkarte", "fr": "Carte de base", "it": "Mappa di base", "en": "Base map" },
          "wmts": "https://wmts.geo.admin.ch/EPSG/2056/1.0.0/WMTSCapabilities.xml",
          "layerIds": ["ch.swisstopo.pixelkarte-grau", "ch.swisstopo.pixelkarte-farbe"],
          "attribution": "swisstopo",
          "attributionUrl": "https://www.swisstopo.admin.ch/de/nutzungsbedingungen-kostenlose-geodaten-und-geodienste"
        },
        {
          "title": { "de": "Vorher", "fr": "Avant", "it": "Prima", "en": "Before" },
          "features": [
            {
              "id": "e9cf8d88-3898-4e2a-869c-32d45e3f21a2",
              "geom": "POLYGON((2600000 1200000,2600010 1200000,2600010 1200010,2600000 1200000))",
              "info": "DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche"
            }
          ],
          "style": "old"
        },
        {
          "title": { "de": "Nachher", "fr": "Après", "it": "Dopo", "en": "After" },
          "features": [
            {
              "id": "e9cf8d88-3898-4e2a-869c-32d45e3f21a2",
              "geom": "POLYGON((2600000 1200000,2600010 1200000,2600010 1200010,2600000 1200000))",
              "info": "DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche"
            }
          ],
          "style": "new"
        }
      ]
    },
    "table": {
      "changes": [
        {
          "id": "c0",
          "objectId": "e9cf8d88-3898-4e2a-869c-32d45e3f21a2",
          "changeType": "changed",
          "valueType": "attribute",
          "interlisName": "DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche",
          "attributePath": "EGID",
          "oldValue": "190001",
          "newValue": "190002"
        }
      ]
    }
  }
}
```
