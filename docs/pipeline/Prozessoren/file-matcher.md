# File Matcher

Filtert eine Liste von Dateien anhand konfigurierbarer Kriterien (Dateiendungen, Dateinamen-Muster) und gibt die übereinstimmenden Dateien als Ausgabe zurück. Im Gegensatz zum [XTF Matcher](xtf-matcher.md) prüft dieser Prozess keine ILI-Modelle in den Datei-Headern und ist deshalb nicht spezifisch für XTF/ITF, sondern für beliebige Dateien einsetzbar (z.B. `pdf`, `png`, `ili`).

Wie beim XTF Matcher werden alle konfigurierten Filter mit UND-Logik kombiniert (eine Datei muss alle aktiven Filter erfüllen). Werte innerhalb eines einzelnen Filters werden mit ODER-Logik kombiniert. Filter, deren Konfiguration `null` oder leer ist, werden übersprungen und schränken das Ergebnis nicht ein.

## Implementierung

Ein File Matcher Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Matcher.FileMatcher.FileMatcherProcess` definieren.

## Konfiguration

- `fileExtensions`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert die Dateiendungen, nach denen gefiltert werden soll (z.B. `pdf`, `png`). Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, wird nicht nach Dateiendung gefiltert.
- `fileNamePatterns`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert reguläre Ausdrücke (Regex), welche gegen den originalen Dateinamen geprüft werden. Mehrere Muster werden mit ODER-Logik zu einer Alternation kombiniert (`(p1)|(p2)|...`). Wenn `null` oder leer, wird nicht nach Dateiname gefiltert.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `files`: Eine Liste von Dateien vom Typ `IPipelineFile[]`, welche gefiltert wird. Die Quelle wird in der Pipeline-Definition festgelegt; in den ausgelieferten Pipelines ist der Parameter mit `${upload()}` (die hochgeladenen Dateien) verdrahtet.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `MatchedFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]`, welche den konfigurierten Filterkriterien entsprechen. Dieses Array kann leer sein, wenn keine Datei den Kriterien entspricht.
- `UnmatchedFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]` mit allen Eingabedateien, welche die Filterkriterien **nicht** erfüllen (die Ergänzung zu `MatchedFiles`). Die Eingabereihenfolge bleibt erhalten. Damit lassen sich die vom Filter aussortierten Dateien in einem nachfolgenden Schritt weiterverarbeiten oder über `output_actions` mitliefern.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche über die Anzahl der übereinstimmenden Dateien informiert (z.B. `"2 von 5 Datei(en) entsprechen den Filterkriterien."`) oder meldet, dass keine Dateien den Kriterien entsprechen.
