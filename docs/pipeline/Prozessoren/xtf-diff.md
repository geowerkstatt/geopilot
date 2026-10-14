# XTF Diff

Listet die Änderungen zwischen einem alten und einem neuen Stand einer INTERLIS-Transferdatei, mit dem [XTF-Diff-Tool](https://github.com/geowerkstatt/XTF-Diff-Tool) über den ilitools-wrapper. Ein Objekt, das nur im alten Stand vorkommt, gilt als gelöscht, eines nur im neuen als hinzugefügt. Bei einem Objekt in beiden Ständen meldet das Tool jedes geänderte Attribut, jede geänderte Geometrie und jede geänderte Referenz.

Der Prozess weiss nicht, woher die beiden Stände kommen: Jeder ist ein eigener Input, und die Pipeline bestimmt, welche Datei welcher Stand ist.

## Implementierung

Ein XTF Diff muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess` definieren.

## Konfiguration

- `modelDirs`: Optional, die INTERLIS-Modell-Repositories als semikolon-getrennter Wert, in dieser Reihenfolge durchsucht: `http(s)`-URLs und Repositories, die der ilitools-wrapper als `%REPOSITORIES/<id>` anbietet. Ohne Angabe gilt `https://models.interlis.ch/`. Platzhalter wie `%ITF_DIR` und mitgelieferte Modelle kennt das Tool nicht. Wie bei der [XTF Validierung](xtf-validierung.md) gehört der Wert in die Basis-Konfiguration der appsettings, `Pipeline:ProcessConfigs:Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess:modelDirs`.

### Was das Tool vergleicht

- Beide Stände müssen dieselbe INTERLIS-Version und dieselben Modelle verwenden, sonst gibt es kein Diff (siehe `ComparisonState`).
- Das Tool ordnet die Objekte der beiden Stände über ihre OID einander zu und vergleicht deshalb nur Klassen und Assoziationen, die im Modell eine OID haben, etwa `OID AS INTERLIS.UUIDOID`. Die übrigen überspringt es, und nur das Log nennt sie (`has no stable OID`). Bei einem Modell ohne OID meldet der Schritt darum keine Änderungen, auch wenn sich die Daten unterscheiden.
- Das Tool liest beide Dateien vollständig in den Speicher.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `oldXtf`: Der alte Stand vom Typ `IPipelineFile`, genau eine Datei.
- `newXtf`: Der neue Stand vom Typ `IPipelineFile`, genau eine Datei.

Kommen beide Stände aus dem Upload, braucht die Pipeline davor einen Schritt, der sie zuordnet, etwa mit dem [Alt/Neu Matcher](alt-neu-matcher.md) (siehe Beispiel). Die Reihenfolge des Uploads taugt dafür nicht, sie ist zufällig. Strenger geht es mit zwei [XTF Matchern](xtf-matcher.md), deren `fileNamePatterns` je eine Rolle erkennen: Dann muss der Lieferant seine Dateien passend benennen, und der Matcher muss `fileNamePatterns` in seiner `default_config` deklarieren, damit ein Schritt den Schlüssel überschreiben darf.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Diff`: Die Änderungen vom alten zum neuen Stand vom Typ `IPipelineFile[]`, eine Datei mit einem JSON-Array im Format des XTF-Diff-Tools ([OutputFileDescription.md](https://github.com/geowerkstatt/XTF-Diff-Tool/blob/main/OutputFileDescription.md)). Jede Änderung trägt ihre Lage im alten und im neuen Stand (`oldLocation`, `newLocation`) als WKT für die Karte, sofern es eine gibt. Leer, wenn sich die Stände nicht vergleichen lassen: `Download` bietet dann nichts an, statt wie bei einer fehlenden Datei den Lauf scheitern zu lassen. Mit `Download` wird es zum Herunterladen angeboten, mit `Delivery` geht es in die Lieferung. Ein nachfolgender Schritt bekommt die Datei auch als einzelnen Parameter, etwa die [XTF Diff Visualisierung](xtf-diff-visualisierung.md), die es als Tabelle und auf einer Karte zeigt.
- `Log`: Das Log des Tools vom Typ `IPipelineFile`, auch wenn sich die Stände nicht vergleichen lassen. Dann nennt es den Grund, auch einen, den die Status-Nachricht nicht kennt, etwa eine doppelte TID. Nur mit `Download` erfährt ihn der Lieferant.
- `ComparisonState`: Ob das Tool die beiden Stände vergleichen konnte:
  - `Compared`: verglichen, `Diff` listet die Änderungen, möglicherweise keine.
  - `NotComparable`: nicht verglichen, weil die Stände verschiedene Modelle oder INTERLIS-Versionen verwenden oder eine Datei fehlerhaft ist, etwa durch eine doppelte TID. `Diff` ist leer. Verschiedene Modelle oder INTERLIS-Versionen nennt die Status-Nachricht, jeden anderen Grund nur das `Log`.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche die Anzahl Änderungen nennt oder dass sich die Stände nicht vergleichen lassen, bei verschiedenen Modellen oder INTERLIS-Versionen auch warum.

`NotComparable` ist kein Fehler des Schrittes: Ob es die Lieferung verhindert, entscheidet die Pipeline mit einer Condition. Vergleicht sie zwei Uploads, ist eine Datei falsch, und eine `fail_conditions` passt. Vergleicht sie mit einer früheren Lieferung, steckt meist ein Modellwechsel dahinter, und eine `warn_conditions` lässt die Lieferung zu. Wie beim [XTF Metadaten-Extraktor](xtf-metadaten.md) empfiehlt sich die Form `!= 'Compared'`, damit ein Tippfehler im Vergleichswert auffällt. Weil auch eine fehlerhafte Datei `NotComparable` ergibt, hält sie unter einer `warn_conditions` erst eine Validierung auf. Nur wenn das Tool gar kein Log schreibt, scheitert der Schritt.

## Beispiel

Zwei hochgeladene Stände, die der [Alt/Neu Matcher](alt-neu-matcher.md) an den Wörtern `alt` oder `old` und `neu` oder `new` im Dateinamen erkennt. Zwei Stände verschiedener Modelle lassen den Vergleich scheitern, weil dann eine Datei falsch ist.

```yaml
processes:
  - id: xtf_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
    default_config:
      fileExtensions:
        - xtf
  - id: old_new_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.OldNewMatcher.OldNewMatcherProcess
    default_config:
      oldWords:
        - alt
        - old
      newWords:
        - neu
        - new
  - id: xtf_diff
    implementation: Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess
pipelines:
  - id: xtf_diff
    display_name:
      de: XTF Vergleich
      en: XTF Comparison
    steps:
      - id: xtf_matching
        display_name:
          de: XTF Zuordnung
          en: XTF Matching
        process_id: xtf_matcher
        input:
          files: "${upload()}"
      - id: old_new
        display_name:
          de: Alter und neuer Stand
          en: Old and New State
        process_id: old_new_matcher
        conditions:
          post:
            fail_conditions:
              - id: one-old-one-new-required
                expression: "Length([old_new.OldFiles]) != 1 || Length([old_new.NewFiles]) != 1"
                message:
                  de: "Für den Vergleich braucht es genau einen alten und einen neuen Stand."
                  en: "The comparison needs exactly one old and one new state."
            warn_conditions:
              - id: decided-by-name-order
                expression: "[old_new.DecidedBy] != 'RoleWords'"
                message:
                  de: "Kein Dateiname sagt, ob er der alte oder der neue Stand ist. Bitte die Zuordnung prüfen."
                  en: "No file name says whether it is the old or the new state. Please check the assignment."
        input:
          files: "${step_output(xtf_matching.XtfFiles)}"
        output_actions:
          - property: StatusMessage
            actions:
              - StatusMessage
      - id: comparison
        display_name:
          de: Vergleich
          en: Comparison
        process_id: xtf_diff
        conditions:
          post:
            fail_conditions:
              - id: states-not-comparable
                expression: "[comparison.ComparisonState] != 'Compared'"
                message:
                  de: "Die beiden Stände lassen sich nicht vergleichen."
                  en: "The two states cannot be compared."
        input:
          oldXtf: "${step_output(old_new.OldFiles)}"
          newXtf: "${step_output(old_new.NewFiles)}"
        output_actions:
          - property: StatusMessage
            actions:
              - StatusMessage
          - property: Diff
            actions:
              - Download
          - property: Log
            actions:
              - Download
```
