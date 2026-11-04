# XTF Diff Visualisierung

Zeigt die Änderungen eines Diffs des [XTF Diff](xtf-diff.md) in der Lieferansicht: eine Tabelle mit den geänderten Objekten und ihren Werten vorher und nachher, und eine Karte mit dem alten und dem neuen Stand. Tabelle und Karte sind verknüpft und lassen sich gemeinsam filtern.

Die Karte zeigt die Lagen des Diffs unverändert, der Prozess rechnet keine Geometrie um. Eine Änderung ohne Lage steht nur in der Tabelle. Hat keine Änderung eine Lage, entfällt die Karte, ohne Änderungen zeigt die Visualisierung einen Hinweis.

## Implementierung

Eine XTF Diff Visualisierung muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfDiffVisualization.XtfDiffVisualizationProcess` definieren.

## Konfiguration

- `baseMapWmtsCapabilitiesUrl`, `baseMapLayerIds`, `baseMapAttribution`, `baseMapAttributionUrl`: Optional, die Hintergrundkarte, gleich wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md). Ein anderer Dienst als swisstopo braucht `baseMapLayerIds`, und sein Host muss in geopilot freigegeben werden, siehe dort [Hintergrundkarte eines anderen Dienstes](xtf-fehlervisualisierung.md#hintergrundkarte-eines-anderen-dienstes).

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
          en: Change visualization
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

Der `Visualization`-Output ist ein Envelope mit dem Diskriminator `xtfDiff`. Die Nutzlast enthält immer die Tabelle (`table`) mit jeder Änderung des Diffs in seiner Reihenfolge (`changes`), die Karte (`map`) nur, sofern eine Änderung eine Lage hat. Die Karte besteht aus der Hintergrundkarte und den Ebenen "Vorher" und "Nachher" mit einem Feature pro Objekt, dessen Id die OID des Objekts ist und das mehrere Lagen als `GEOMETRYCOLLECTION` trägt; über diese Id verweisen die Änderungen auf ihre Features. `state` sagt dem Frontend, welchen Zustand des Diffs eine Ebene zeigt: `old` oder `new`, gezeichnet nach der Art jeder Änderung; eine Ebene ohne `state` zeichnet es in der Standardfarbe der Karte. Eine Geometrieänderung trägt zudem ihre Lagen (`oldLocation`, `newLocation`), damit die Karte zu ihr allein zoomt und jeden Teil eines Features seiner Änderung zuordnet; die übrigen Änderungen tragen keine. Listen und Strukturen stehen in `oldValue` und `newValue` als kompaktes JSON. Serialisiert wird als camelCase-JSON, `null`-Felder werden weggelassen:

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
          "state": "old"
        },
        {
          "title": { "de": "Nachher", "fr": "Après", "it": "Dopo", "en": "After" },
          "features": [
            {
              "id": "e9cf8d88-3898-4e2a-869c-32d45e3f21a2",
              "geom": "POLYGON((2600000 1200000,2600012 1200000,2600012 1200010,2600000 1200000))",
              "info": "DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche"
            }
          ],
          "state": "new"
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
        },
        {
          "id": "c1",
          "objectId": "e9cf8d88-3898-4e2a-869c-32d45e3f21a2",
          "changeType": "changed",
          "valueType": "geometry",
          "interlisName": "DMAV_Bodenbedeckung_V1_0.Bodenbedeckung.BoFlaeche",
          "attributePath": "Geometrie",
          "oldValue": "POLYGON((2600000 1200000,2600010 1200000,2600010 1200010,2600000 1200000))",
          "newValue": "POLYGON((2600000 1200000,2600012 1200000,2600012 1200010,2600000 1200000))",
          "oldLocation": "POLYGON((2600000 1200000,2600010 1200000,2600010 1200010,2600000 1200000))",
          "newLocation": "POLYGON((2600000 1200000,2600012 1200000,2600012 1200010,2600000 1200000))"
        }
      ]
    }
  }
}
```
