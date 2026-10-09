# XTF Diff

Listet die Änderungen zwischen einem alten und einem neuen Stand einer INTERLIS-Transferdatei, mit dem [XTF-Diff-Tool](https://github.com/geowerkstatt/XTF-Diff-Tool) über den ilitools-wrapper. Ein Objekt, das nur im alten Stand vorkommt, gilt als gelöscht, eines nur im neuen als hinzugefügt. Bei einem Objekt in beiden Ständen meldet das Tool jedes geänderte Attribut, jede geänderte Geometrie und jede geänderte Referenz.

Der Prozess weiss nicht, woher die beiden Stände kommen: Jeder ist ein eigener Input, und die Pipeline bestimmt, welche Datei welcher Stand ist.

## Implementierung

Ein XTF Diff muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess` definieren.

## Konfiguration

- `modelDirs`: Optional, die INTERLIS-Modell-Repositories als semikolon-getrennter Wert, in dieser Reihenfolge durchsucht: `http(s)`-URLs und Repositories, die der ilitools-wrapper als `%REPOSITORIES/<id>` anbietet. Ohne Angabe gilt `https://models.interlis.ch/`. Platzhalter wie `%ITF_DIR` und mitgelieferte Modelle kennt das Tool nicht. Wie bei der [XTF Validierung](xtf-validierung.md) gehört der Wert in die Basis-Konfiguration der appsettings, `Pipeline:ProcessConfigs:Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess:modelDirs`.

### Was das Tool vergleicht

- Beide Stände müssen dieselbe INTERLIS-Version und dieselben Modelle verwenden, sonst gibt es kein Diff (siehe `ComparisonState`).
- Das Tool ordnet die Objekte der beiden Stände über ihre OID einander zu und vergleicht deshalb nur Klassen und Assoziationen, die im Modell eine OID haben, etwa `OID AS INTERLIS.UUIDOID`. Die übrigen überspringt es, und nur das Log nennt sie (`has no stable OID`). Bei einem Modell ohne OID meldet der Schritt darum keine Änderungen, auch wenn sich die Daten unterscheiden. Die DMAV-Modelle haben OIDs.
- Das Tool liest beide Dateien vollständig in den Speicher.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `oldXtf`: Der alte Stand vom Typ `IPipelineFile`, genau eine Datei.
- `newXtf`: Der neue Stand vom Typ `IPipelineFile`, genau eine Datei.

Kommen beide Stände aus dem Upload, braucht es vorgelagerte Schritte, die sie dem alten und dem neuen Stand zuordnen, etwa zwei [XTF Matcher](xtf-matcher.md) mit `fileNamePatterns` (siehe Beispiel). Die Reihenfolge des Uploads taugt dafür nicht, sie ist zufällig.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Diff`: Die Änderungen vom alten zum neuen Stand vom Typ `IPipelineFile`, ein JSON-Array im Format des XTF-Diff-Tools ([OutputFileDescription.md](https://github.com/geowerkstatt/XTF-Diff-Tool/blob/main/OutputFileDescription.md)). Fehlt, wenn sich die Stände nicht vergleichen lassen. Mit `Download` wird es zum Herunterladen angeboten, mit `Delivery` geht es in die Lieferung.
- `Log`: Das Log des Tools vom Typ `IPipelineFile`, auch wenn sich die Stände nicht vergleichen lassen.
- `ComparisonState`: Ob das Tool die beiden Stände vergleichen konnte:
  - `Compared`: verglichen, `Diff` listet die Änderungen, möglicherweise keine.
  - `NotComparable`: Die Stände verwenden verschiedene Modelle oder INTERLIS-Versionen, es gibt kein `Diff`.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche die Anzahl Änderungen nennt, oder warum sich die Stände nicht vergleichen lassen.

`NotComparable` ist kein Fehler des Schrittes: Ob es die Lieferung verhindert, entscheidet die Pipeline mit einer Condition. Vergleicht sie zwei Uploads, ist eine Datei falsch, und eine `fail_conditions` passt. Vergleicht sie mit einer früheren Lieferung, steckt meist ein Modellwechsel dahinter, und eine `warn_conditions` lässt die Lieferung zu. Wie beim [XTF Metadaten-Extraktor](xtf-metadaten.md) empfiehlt sich die Form `!= 'Compared'`, damit ein Tippfehler im Vergleichswert auffällt. Jeder andere Fehler des Tools, etwa eine unlesbare Datei, lässt den Schritt scheitern.

## Beispiel

Zwei hochgeladene Stände, die zwei Matcher an den Wörtern `alt` oder `old` und `neu` oder `new` im Dateinamen erkennen. Nur ganze Wörter zählen, `Altlasten.xtf` enthält kein `alt`. Damit ein Schritt `fileNamePatterns` überschreiben kann, muss der Matcher den Schlüssel in seiner `default_config` deklarieren.

```yaml
processes:
  - id: xtf_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
    default_config:
      fileExtensions:
        - xtf
      fileNamePatterns:
  - id: xtf_diff
    implementation: Geopilot.Pipeline.Processes.XtfDiff.XtfDiffProcess
pipelines:
  - id: xtf_diff
    display_name:
      de: XTF Vergleich
      en: XTF Comparison
    steps:
      - id: old_xtf
        display_name:
          de: Alter Stand
          en: Old State
        process_id: xtf_matcher
        process_config_overwrites:
          fileNamePatterns:
            - '(?i)(^|[^a-z0-9])(alt|old)([^a-z0-9]|$)'
        input:
          files: "${upload()}"
      - id: new_xtf
        display_name:
          de: Neuer Stand
          en: New State
        process_id: xtf_matcher
        process_config_overwrites:
          fileNamePatterns:
            - '(?i)(^|[^a-z0-9])(neu|new)([^a-z0-9]|$)'
        input:
          files: "${upload()}"
      - id: comparison
        display_name:
          de: Vergleich
          en: Comparison
        process_id: xtf_diff
        conditions:
          pre:
            fail_conditions:
              - id: one-old-and-one-new-state
                expression: "Length([old_xtf.XtfFiles]) != 1 || Length([new_xtf.XtfFiles]) != 1"
                message:
                  de: "Es braucht genau eine XTF-Datei mit alt oder old und genau eine mit neu oder new im Namen."
                  en: "Exactly one XTF file with old or alt and exactly one with new or neu in its name is required."
          post:
            fail_conditions:
              - id: states-not-comparable
                expression: "[comparison.ComparisonState] != 'Compared'"
                message:
                  de: "Die beiden Stände lassen sich nicht vergleichen."
                  en: "The two states cannot be compared."
        input:
          oldXtf: "${step_output(old_xtf.XtfFiles)}"
          newXtf: "${step_output(new_xtf.XtfFiles)}"
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
