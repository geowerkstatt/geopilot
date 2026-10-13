# Alt/Neu Matcher

Ordnet Dateien einem alten und einem neuen Stand zu, damit ein Prozess, der zwei Stände vergleicht, jeden Stand aus einem eigenen Input nehmen kann, etwa der [XTF Diff](xtf-diff.md). Die Reihenfolge des Uploads taugt dafür nicht, sie ist zufällig. Stattdessen entscheiden Rollenwörter im Dateinamen, etwa `alt` und `neu`. Nur zwei Dateien ganz ohne Rollenwort ordnet die Reihenfolge der Namen zu. Wie viele Dateien ein Stand haben darf, entscheidet die Pipeline mit einer Bedingung auf die Anzahl (siehe Beispiel), der Prozess selbst scheitert nie an der Anzahl.

## Implementierung

Ein Alt/Neu Matcher muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Matcher.OldNewMatcher.OldNewMatcherProcess` definieren.

## Konfiguration

- `oldWords`: Optionaler Parameter vom Typ `HashSet<string>`. Wörter, die im Dateinamen den alten Stand kennzeichnen, etwa `alt` und `old`. Eine Liste, sie gehört darum in die Pipeline-Definition (`default_config`), nicht in die Basis-Konfiguration der appsettings.
- `newWords`: Optionaler Parameter vom Typ `HashSet<string>`. Wörter, die den neuen Stand kennzeichnen, etwa `neu` und `new`.

Ohne Rollenwörter ordnet der Prozess nur zwei Dateien zu, nach der Reihenfolge der Namen. Weitere Sprachen oder Paare wie `vorher` und `nachher` kommen einfach in die Listen.

### Regel

1. Jeder Dateiname wird an `_`, `-`, `.` und Leerzeichen in Wörter zerlegt und ohne Gross- und Kleinschreibung mit den Rollenwörtern verglichen. Nur ganze Wörter zählen: `Altlasten.xtf` enthält kein `alt`.
2. Eine Datei mit Wörtern genau einer Rolle bekommt diese Rolle. Eine Datei mit Wörtern beider Rollen bekommt keine.
3. Bei genau zwei Dateien entscheidet eine Datei mit Rollenwort auch für die andere, wenn diese gar kein Rollenwort trägt: `plan_alt.xtf` und `plan.xtf` ergeben `plan_alt.xtf` als alten und `plan.xtf` als neuen Stand.
4. Tragen genau zwei Dateien beide kein Rollenwort, entscheiden die Namen: aufsteigend sortiert, ohne Gross- und Kleinschreibung, die erste Datei ist der alte Stand. Aufsteigend, damit ein Datum im Namen (`2025` vor `2026`) in dieselbe Richtung zeigt wie die Rollenwörter.
5. Sonst bleibt es bei den Rollen aus Punkt 2, Dateien ohne Rolle landen in `UnmatchedFiles`. Widersprüchliche Rollenwörter fallen nie auf die Namen zurück: Zwei Dateien mit `neu` lassen den alten Stand leer, und die Bedingung auf die Anzahl lässt den Schritt scheitern.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `files`: Die Dateien vom Typ `IPipelineFile[]`, in der Regel die Treffer eines [XTF Matchers](xtf-matcher.md).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `OldFiles`: Die Dateien des alten Stands vom Typ `IPipelineFile[]`, in der Eingabereihenfolge. Das Array kann leer sein oder mehrere Dateien enthalten.
- `NewFiles`: Die Dateien des neuen Stands vom Typ `IPipelineFile[]`, ebenso.
- `UnmatchedFiles`: Die Dateien ohne Rolle vom Typ `IPipelineFile[]`, also ohne Rollenwort oder mit Wörtern beider Rollen. Die Eingabereihenfolge bleibt erhalten.
- `DecidedBy`: Was die Zuordnung entschieden hat:
  - `RoleWords`: die Rollenwörter, auch wenn sie einen Stand leer oder mehrfach besetzt lassen.
  - `NameOrder`: die Reihenfolge der Namen zweier Dateien ohne Rollenwort.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche die Dateien beider Stände nennt, `keine Datei` für einen leeren Stand, bei `NameOrder` mit dem Hinweis, dass nach Namen sortiert wurde.

Ein Vergleich nimmt je Stand genau eine Datei. Ein Input vom Typ `IPipelineFile` nimmt zwar ein Array mit genau einer Datei an, bei keiner oder mehreren scheitert er aber mit einer technischen Meldung. Darum verlangt eine `fail_conditions` nach dem Schritt genau eine Datei pro Stand und gibt dafür eine verständliche Meldung (siehe Beispiel). Ob übrige Dateien in `UnmatchedFiles` stören, entscheidet die Pipeline ebenso, etwa mit `Length([old_new.UnmatchedFiles]) > 0`.

Eine Zuordnung nach den Namen ist geraten. Damit sie auffällt, schliesst die Pipeline den Schritt dann mit einer `warn_conditions` auf `!= 'RoleWords'` als Warnung ab (siehe Beispiel). Die Warnung verhindert keine Lieferung. In dieser Form lässt ein Tippfehler im Vergleichswert die Warnung immer erscheinen, statt sie still wirkungslos zu machen.

## Beispiel

Zwei hochgeladene Stände, verglichen mit dem [XTF Diff](xtf-diff.md):

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
        input:
          oldXtf: "${step_output(old_new.OldFiles)}"
          newXtf: "${step_output(old_new.NewFiles)}"
        output_actions:
          - property: Diff
            actions:
              - Download
```
