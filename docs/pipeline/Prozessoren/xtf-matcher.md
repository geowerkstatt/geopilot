# XTF Matcher

Filtert eine Liste von Dateien anhand konfigurierbarer Kriterien und gibt die übereinstimmenden Dateien als Ausgabe zurück. Alle konfigurierten Filter werden mit UND-Logik kombiniert: Eine Datei muss alle aktiven Filter erfüllen, um in der Ausgabe enthalten zu sein. Innerhalb eines einzelnen Filters werden die Werte mit ODER-Logik kombiniert (z.B. Dateiendungen `xtf` und `itf` stimmen mit einer der beiden überein). Filter, deren Konfiguration `null` oder leer ist, werden übersprungen und schränken das Ergebnis nicht ein.

## Implementierung

Ein XTF Matcher Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess` definieren.

## Konfiguration

- `fileExtensions`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert die Dateiendungen, nach denen gefiltert werden soll (z.B. `xtf`, `itf`). Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, wird nicht nach Dateiendung gefiltert.
- `iliModels`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert die ILI-Modellnamen, nach denen im Kopf der Transferdatei gefiltert werden soll. Unterstützt werden INTERLIS 2.4 (Elemente mit `ili:`-Präfix, Modellname als Elementtext), INTERLIS 2.3 (Elemente in Grossbuchstaben im Default-Namespace, Modellname im `NAME`-Attribut) und INTERLIS 1 (Modellname in der `MODL`-Zeile). Welches Format gilt, entscheidet wie bei der [XTF Validierung](xtf-validierung.md) die Dateiendung: `itf` ist INTERLIS 1, jede andere Endung XTF. Dateien, deren Kopf sich nicht lesen lässt, werden bei aktiviertem Modellfilter ausgeschlossen. Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, wird nicht nach ILI-Modell gefiltert.
- `fileNamePatterns`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert reguläre Ausdrücke (Regex), welche gegen den originalen Dateinamen geprüft werden (z.B. `Road.*`, `Map.*`). Der Vergleich ist case-sensitive. Wenn `null` oder leer, wird nicht nach Dateiname gefiltert.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `files`: Eine Liste von Dateien vom Typ `IPipelineFile[]`, welche gefiltert wird. Die Quelle wird in der Pipeline-Definition festgelegt; in den ausgelieferten Pipelines ist der Parameter mit `${upload()}` (die hochgeladenen Dateien) verdrahtet.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `XtfFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]`, welche den konfigurierten Filterkriterien entsprechen. Dieses Array kann leer sein, wenn keine Datei den Kriterien entspricht.
- `UnmatchedFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]` mit allen Eingabedateien, welche die Filterkriterien **nicht** erfüllen (die Ergänzung zu `XtfFiles`). Die Eingabereihenfolge bleibt erhalten. Der Rest wird nach dem inhaltsbasierten Modellfilter berechnet, weshalb Dateien, deren Kopf sich nicht lesen lässt, bei aktiviertem `iliModels`-Filter hier landen. Damit lassen sich die aussortierten Dateien in einem nachfolgenden Schritt weiterverarbeiten oder über `output_actions` mitliefern.
- `InterlisVersions`: Die INTERLIS-Versionen der Dateien in `XtfFiles` als Liste von Texten, jede Version einmal und aufsteigend sortiert, mit den Werten `Interlis1` (ITF), `Interlis23` und `Interlis24`. Erkannt wird die Version am Dateianfang: eine ITF-Datei an der ersten Zeile `SCNT`, eine XTF-Datei am Namespace ihres Wurzelelements. Eine Datei, die keiner dieser Versionen zugeordnet werden kann (auch INTERLIS 2.2), trägt nichts bei. Entspricht keine Datei den Filterkriterien, ist die Liste leer. Welche Datei welche Version hat, gibt der Matcher nicht an. In einer Condition fragt der Operator `in` nach einer Version, z.B. `'Interlis23' in [xtf_matching.InterlisVersions]`, und `Length([xtf_matching.InterlisVersions])` zählt sie.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche über die Anzahl der übereinstimmenden Dateien informiert (z.B. `"1 von 3 Datei(en) entsprechen den XTF-Filterkriterien."`) oder meldet, dass keine Dateien den Kriterien entsprechen.

## Beispiel: Validierungsprofil je INTERLIS-Version

Ein Input kann keine Fallunterscheidung ausdrücken. Soll die Validierung je nach INTERLIS-Version mit einem anderen Profil laufen, erhält jede Version einen eigenen Validierungsschritt, welcher mit einer `skip_conditions` übersprungen wird, wenn seine Version nicht vorkommt. Erkennt der Matcher keine Version, würden alle Validierungsschritte übersprungen und die Pipeline schlösse ohne Validierung erfolgreich ab, mit lieferbaren Daten. Die Bedingung `interlis-version-unknown` auf dem Matcher-Schritt verhindert das. Weil ein Validierungsschritt genau eine Transferdatei nimmt, verlangt die Bedingung `single-xtf-file` genau eine XTF-Datei. Sonst scheiterte der Validierungsschritt erst zur Laufzeit beim Binden seines Inputs.

```yaml
processes:
  - id: xtf_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
    default_config:
      fileExtensions:
        - xtf
  - id: xtf_validator
    implementation: Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess
    default_config:
      validationProfile:
pipelines:
  - id: xtf_validation
    display_name:
      de: XTF-Validierung
    steps:
      - id: xtf_matching
        display_name:
          de: XTF-Dateien finden
        process_id: xtf_matcher
        conditions:
          post:
            fail_conditions:
              - id: single-xtf-file
                expression: "Length([xtf_matching.XtfFiles]) != 1"
                message:
                  de: "Es wird genau eine XTF-Datei erwartet."
                  en: "Exactly one XTF file is expected."
              - id: interlis-version-unknown
                expression: "Length([xtf_matching.InterlisVersions]) == 0"
                message:
                  de: "Die INTERLIS-Version wurde nicht erkannt."
                  en: "The INTERLIS version was not recognized."
        input:
          files: "${upload()}"
      - id: validation_23
        display_name:
          de: Validierung INTERLIS 2.3
        process_id: xtf_validator
        process_config_overwrites:
          validationProfile: ILI_23
        conditions:
          pre:
            skip_conditions:
              - expression: "!('Interlis23' in [xtf_matching.InterlisVersions])"
          post:
            restrict_delivery_conditions:
              - expression: "!([validation_23.ValidationSuccessful])"
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
      - id: validation_24
        display_name:
          de: Validierung INTERLIS 2.4
        process_id: xtf_validator
        process_config_overwrites:
          validationProfile: ILI_24
        conditions:
          pre:
            skip_conditions:
              - expression: "!('Interlis24' in [xtf_matching.InterlisVersions])"
          post:
            restrict_delivery_conditions:
              - expression: "!([validation_24.ValidationSuccessful])"
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
```

Ein nachfolgender Schritt erhält die Outputs beider Validierungsschritte als Liste von Quellen, z.B. `xtfLog: ["${step_output(validation_23.XtfLog)}", "${step_output(validation_24.XtfLog)}"]`. Der übersprungene Schritt trägt nichts bei (siehe [Aufbau der Pipeline-Definition](../Pipelines.md#wie-ist-eine-pipeline-definition-aufgebaut)).
