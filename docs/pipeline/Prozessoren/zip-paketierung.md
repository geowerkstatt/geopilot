# ZIP Paketierung

Nimmt eine Liste von Dateien entgegen und packt sie in ein ZIP-Archiv, welches als Ausgabe bereitgestellt wird. Sowohl Input als auch Output sind vom Typ `IPipelineFile`, welches die Dateien bzw. das ZIP-Archiv enthält. Die ursprüngliche Verzeichnisstruktur der Eingabedateien wird anhand von `IPipelineFile.OriginalRelativePath` im ZIP-Archiv beibehalten, Dateien mit einem gesetzten Pfad (z.B. aus dem ZIP Unpacker) werden unter dem entsprechenden Verzeichnis im Archiv abgelegt.

## Implementierung

Ein ZIP Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess` definieren.

## Konfiguration

- `archiveFileName`: Optionaler Parameter, welcher den Namen des ZIP-Archivs definiert, welches erstellt wird. Wenn dieser Parameter nicht definiert ist, wird dem Archiv der Name `archive.zip` vergeben.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `input`: Eine Liste von Input-Files vom Typ `IPipelineFile?`. Auf diesen Namen können 0-n Files gemappt werden, welche in das ZIP-Archiv gepackt werden sollen. `null` als ein Input-File in der Liste ist erlaubt und wird vom Prozessor einfach ignoriert.

Falls Dateien mit identischem Pfad (Kombination aus `OriginalRelativePath` und `OriginalFileName`) vorhanden sind, wird eine Warnung im Log ausgegeben. Dateien mit gleichem Namen in unterschiedlichen Verzeichnissen gelten nicht als Duplikate. Das ZIP-Archiv wird trotzdem erstellt, wobei echte Duplikate zu mehreren Einträgen mit dem gleichen Pfad im Archiv führen.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `ZipPackage`: Ein Output-File vom Typ `IPipelineFile`, welches das erstellte ZIP-Archiv enthält. Dieses Archiv enthält alle Dateien, welche über den Input `input` an den Prozess übergeben wurden. Falls die Liste an Input-Files nur `null`-Werte enthält, ist dieser Output `null`.
- `StatusMessage`: Eine lokalisierte Status-Nachricht der Validierung vom Typ `LocalizedText`, welche über den Status des ZIP-Prozesses informiert. Die Status-Nachricht kann auch ein leerer String sein.
