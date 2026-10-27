# XTF Diff Visualisierung

Zeigt die Änderungen eines Diffs des [XTF Diff](xtf-diff.md) in der Lieferansicht: die geänderten Objekte in einer Liste und auf einer Karte. Die Karte zeichnet jede Änderung in Farbe und Form ihrer Art, damit sie auch ohne Farbe unterscheidbar bleibt: hinzugefügt grün mit gepunkteter Linie oder Umrandung, gelöscht rot mit gestrichelter, geändert blau mit dem alten Stand als breites Band oder Ring unter dem neuen; ein verschobener Punkt ist mit seiner alten Lage durch eine Linie verbunden. Eine Geometrie, die einem geänderten Objekt hinzugefügt oder entfernt wurde, erscheint als hinzugefügt oder gelöscht, wie ihre Zeile in der Liste. Eine Legende auf der Karte zeigt diese Darstellungen für Punkte, Linien und Flächen, und ein Schieberegler auf der Karte blendet vom alten zum neuen Stand über. Jedes Objekt erscheint in der Liste einmal, mit Klasse, Objekt-Id und einer einzeiligen Zusammenfassung: hinzugefügt oder gelöscht als Ganzes, oder die Namen der geänderten Eigenschaften, hinzugefügte grün mit Plus, entfernte rot mit Minus. Aufgeklappt zeigt ein Objekt jede Änderung mit dem Wert vorher und nachher, nebeneinander oder, im Vollbild und auf dem Telefon, untereinander; markiert ist nur der Teil eines Werts, der sich geändert hat. Werte über vier Zeilen lassen sich ganz anzeigen. Eine Referenz auf ein anderes geändertes Objekt springt zu diesem, klappt es auf und fokussiert es; eine Referenz auf ein Objekt ausserhalb des Diffs oder eines, das der Filter ausblendet, ist kein Link. Liste und Karte sind verknüpft wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md): Der Filter nach Text, Änderungsart, Werttyp, Klasse und Attribut wirkt auf beide, ein Objekt auf der Karte hat in der Liste einen Knopf, der zu ihm zoomt und die Karte ins Bild scrollt; die Knöpfe neben einem Geometriewert zoomen zu dieser Geometrie vor oder nach der Änderung und umranden sie zusammen mit ihrem Gegenstück im anderen Stand. Ein Klick auf der Karte zeigt das Objekt in der Liste und in einem Popup, ein Klick neben die Objekte hebt die Auswahl auf. Im Vollbild liegen Filter und Liste neben der Karte.

Die Karte zeigt die Lagen des Diffs (`oldLocation`, `newLocation`) unverändert, der Prozess rechnet keine Geometrie um. Ein Objekt erscheint in den Ebenen "Vorher" und "Nachher" je höchstens einmal, auch wenn es mehrere Änderungen hat: ein gelöschtes nur unter "Vorher", ein hinzugefügtes nur unter "Nachher". Haben seine Änderungen verschiedene Lagen, etwa eine geänderte Geometrie in einer Struktur neben der Geometrie des Objekts, fasst die Karte sie zu einer `GEOMETRYCOLLECTION` zusammen, so wie das Tool ein Objekt mit mehreren Geometrie-Attributen schreibt. Eine Änderung ohne Lage, etwa an einem Objekt ohne eigene Geometrie oder aus einem Diff ohne Lagen, steht nur in der Liste. Hat keine Änderung eine Lage, entfällt die Karte. Ohne Änderungen zeigt die Visualisierung einen Hinweis.

## Implementierung

Eine XTF Diff Visualisierung muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfDiffVisualization.XtfDiffVisualizationProcess` definieren.

## Konfiguration

- `include`: Optionaler Parameter vom Typ `IReadOnlyList<DiffView>`, die gezeigten Ansichten wie bei der [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md): `map` (die Karte mit dem Popup) und `table` (die Liste der Objekte mit dem Filter). Die Gross-/Kleinschreibung ist unerheblich, ein anderer Wert lässt die Validierung beim Applikationsstart scheitern. Wenn `null` oder leer, werden beide gezeigt. Ohne Liste verhält sich die Karte wie die Karte der Fehlervisualisierung ohne Baum: Das Popup nennt nur die Klasse der Objekte, und es gibt keinen Filter. Ist nur die Karte gewählt und hat keine Änderung eine Lage, zeigt die Visualisierung einen Hinweis.
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

Der `Visualization`-Output ist ein Envelope mit dem Diskriminator `xtfDiff`. Wie bei der Fehlervisualisierung enthält die Nutzlast nur die Ansichten, die erscheinen: die Liste (`table`) mit jeder Änderung des Diffs in seiner Reihenfolge (`changes`), sofern `include` sie enthält, und die Karte (`map`), sofern `include` sie enthält und eine Änderung eine Lage hat. Die Karte besteht aus der Hintergrundkarte und den Ebenen "Vorher" und "Nachher" mit einem Feature pro Objekt, dessen Id die OID des Objekts ist und das mehrere Lagen als `GEOMETRYCOLLECTION` trägt; über diese Id verweisen die Änderungen auf ihre Features. `style` sagt dem Frontend, wie es eine Ebene zeichnet: `old` und `new` nach der Art jeder Änderung, ohne `style` in der Standardfarbe der Karte. Ohne Liste kennt die Karte die Arten nicht und leitet sie aus den Ebenen ab: nur unter "Vorher" gelöscht, nur unter "Nachher" hinzugefügt, unter beiden geändert. Eine Geometrieänderung trägt zudem ihre Lagen (`oldLocation`, `newLocation`), sofern die Karte erscheint, damit die Karte zu ihr allein zoomt und jeden Teil eines Features seiner Änderung zuordnet; die übrigen Änderungen tragen keine. Listen und Strukturen stehen in `oldValue` und `newValue` als kompaktes JSON. Serialisiert wird als camelCase-JSON, `null`-Felder werden weggelassen:

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
              "geom": "POLYGON((2600000 1200000,2600012 1200000,2600012 1200010,2600000 1200000))",
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
