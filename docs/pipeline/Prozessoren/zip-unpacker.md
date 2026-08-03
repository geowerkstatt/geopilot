# ZIP Unpacker

Entpackt ein einzelnes ZIP-Archiv und stellt dessen Dateien als flachen Array von `IPipelineFile` zur Verfügung. Die ursprüngliche Verzeichnisstruktur des Archivs bleibt als Metadaten via `IPipelineFile.OriginalRelativePath` erhalten.

## Implementierung

Ein ZIP Entpacker Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Unzip.UnzipProcess` definieren.

## Konfiguration

Keine spezifische Konfiguration.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `zipFile`: Ein Input-File vom Typ `IPipelineFile`. Auf diesen Namen muss genau ein File gemappt werden, welches das zu entpackende ZIP-Archiv enthält.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `ExtractedFiles`: Ein Array von Dateien vom Typ `IPipelineFile[]`, welche dem Inhalt des ZIP-Archivs entsprechen. Auf jeder Datei steht via `OriginalRelativePath` der Verzeichnispfad innerhalb des Archivs (oder ein leerer String für Dateien auf der obersten Ebene). Dieses Array kann leer sein, wenn das Archiv keine Dateien enthält.
- `StatusMessage`: Eine lokalisierte Status-Nachricht des Entpackers vom Typ `LocalizedText`, welche über die Anzahl der entpackten Dateien bzw. ein leeres Archiv informiert.
