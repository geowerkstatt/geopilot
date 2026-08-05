# XTF Matcher

Filtert eine Liste von Dateien anhand konfigurierbarer Kriterien und gibt die übereinstimmenden Dateien als Ausgabe zurück. Alle konfigurierten Filter werden mit UND-Logik kombiniert: Eine Datei muss alle aktiven Filter erfüllen, um in der Ausgabe enthalten zu sein. Innerhalb eines einzelnen Filters werden die Werte mit ODER-Logik kombiniert (z.B. Dateiendungen `xtf` und `itf` stimmen mit einer der beiden überein). Filter, deren Konfiguration `null` oder leer ist, werden übersprungen und schränken das Ergebnis nicht ein.

## Implementierung

Ein XTF Matcher Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess` definieren.

## Konfiguration

- `fileExtensions`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert die Dateiendungen, nach denen gefiltert werden soll (z.B. `xtf`, `itf`). Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, wird nicht nach Dateiendung gefiltert.
- `iliModels`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert die ILI-Modellnamen, nach denen in den XTF-Header-Metadaten gefiltert werden soll. Unterstützt werden sowohl INTERLIS 2.4 (Elemente mit `ili:`-Präfix, Modellname als Elementtext) als auch INTERLIS 2.3 (Elemente in Grossbuchstaben im Default-Namespace, Modellname im `NAME`-Attribut). Dateien, welche nicht als XTF geparst werden können, werden bei aktiviertem Modellfilter ausgeschlossen. Der Vergleich erfolgt ohne Berücksichtigung der Gross-/Kleinschreibung. Wenn `null` oder leer, wird nicht nach ILI-Modell gefiltert.
- `fileNamePatterns`: Optionaler Parameter vom Typ `HashSet<string>`. Definiert reguläre Ausdrücke (Regex), welche gegen den originalen Dateinamen geprüft werden (z.B. `Road.*`, `Map.*`). Der Vergleich ist case-sensitive. Wenn `null` oder leer, wird nicht nach Dateiname gefiltert.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `files`: Eine Liste von Dateien vom Typ `IPipelineFile[]`, welche gefiltert wird. Die Quelle wird in der Pipeline-Definition festgelegt; in den ausgelieferten Pipelines ist der Parameter mit `${upload()}` (die hochgeladenen Dateien) verdrahtet.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `XtfFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]`, welche den konfigurierten Filterkriterien entsprechen. Dieses Array kann leer sein, wenn keine Datei den Kriterien entspricht.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche über die Anzahl der übereinstimmenden Dateien informiert (z.B. `"1 von 3 Datei(en) entsprechen den XTF-Filterkriterien."`) oder meldet, dass keine Dateien den Kriterien entsprechen.
