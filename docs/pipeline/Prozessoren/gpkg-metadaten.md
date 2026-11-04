# GPKG Metadaten-Extraktor

Liest Metadaten aus einer GeoPackage-Datei, damit nachfolgende Schritte damit arbeiten können. In dieser Fassung sind das die INTERLIS-Modelle, die in der Datei gespeichert sind. Sie lassen sich etwa an die [GPKG Validierung](gpkg-validierung.md) weitergeben, damit diese die gesamten Daten der Datei prüft.

Die Modelle stehen in der Tabelle `T_ILI2DB_MODEL`, die ili2gpkg beim Schema-Import anlegt (siehe [GPKG Schema-Import](gpkg-schema-import.md)). Ein GeoPackage, das nicht mit ili2gpkg erstellt wurde, enthält diese Tabelle nicht. Die Datei wird nur gelesen und nicht verändert.

## Implementierung

Ein GPKG Metadaten-Extraktor muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Gpkg.GpkgMetadataExtractorProcess` definieren.

## Konfiguration

Der Prozess hat keine Konfiguration.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `gpkgFile`: Die GeoPackage-Datei vom Typ `IPipelineFile`, genau eine. In der Regel mit `${step_output(file_matching.MatchedFiles)}` verdrahtet, zusammen mit einer Nachbedingung am Matcher, dass er genau eine GPKG-Datei gefunden hat (siehe Beispiel).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Models`: Die Namen der gespeicherten Modelle vom Typ `string[]` (`["Geometry_V2", "GeometryCHLV95_V2"]`), leer, wenn keine gelesen werden konnten. Dazu gehören auch die importierten Basismodelle wie `Units` oder `CoordSys`. Jedes Modell kommt nur einmal vor. Die Liste passt direkt auf den Input `models` der GPKG Validierung. Eine Einschränkung, dass ein bestimmtes Modell enthalten sein muss, kann über eine Condition gelöst werden, siehe [Beispiel](#beispiel).
- `FileState`: Wie weit die Datei gelesen werden konnte:
  - `Readable`: Die Modelle wurden gelesen.
  - `MissingData`: Die Datei ist eine lesbare SQLite-Datenbank, aber die Tabelle `T_ILI2DB_MODEL` fehlt oder ist leer.
  - `Unreadable`: Die Datei ist keine lesbare SQLite-Datenbank, etwa weil ein falscher Dateityp hochgeladen wurde, oder die Tabelle `T_ILI2DB_MODEL` liess sich nicht lesen oder beinhaltet ungültige Modellnamen.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche die gelesenen Modelle nennt oder erklärt, warum keine gelesen wurden.

Keine Modelle sind kein Fehler des Schrittes: Ob sie Pflicht sind, entscheidet die Pipeline mit einer Condition. Für `FileState` empfiehlt sich die Form `!= 'Readable'`. Sie deckt beide Fehlerzustände ab, und ein Tippfehler im Vergleichswert lässt die Condition immer greifen, statt sie still wirkungslos zu machen.

## Beispiel

```yaml
processes:
  - id: gpkg_metadata_extractor
    implementation: Geopilot.Pipeline.Processes.Gpkg.GpkgMetadataExtractorProcess
  - id: gpkg_data_validator
    implementation: Geopilot.Pipeline.Processes.Gpkg.Ili2GpkgValidatorProcess
  # file_matcher wie in den übrigen Pipelines
pipelines:
  - id: gpkg_validation
    display_name:
      de: GPKG-Datenvalidierung
      en: GPKG Data Validation
    steps:
      - id: file_matching
        display_name:
          de: Hochgeladene Dateien werden analysiert
          en: Analyzing uploaded files
        process_id: file_matcher
        process_config_overwrites:
          fileExtensions:
            - gpkg
        conditions:
          post:
            fail_conditions:
              - id: one-gpkg-required
                expression: "Length([file_matching.MatchedFiles]) != 1"
                message:
                  de: "Es muss genau eine GPKG-Datei hochgeladen werden."
                  en: "Exactly one GPKG file must be uploaded."
        input:
          files: "${upload()}"
      - id: gpkg_metadata
        display_name:
          de: GPKG-Metadaten werden extrahiert
          en: Extracting GPKG metadata
        process_id: gpkg_metadata_extractor
        conditions:
          post:
            fail_conditions:
              - id: gpkg-not-readable
                expression: "[gpkg_metadata.FileState] != 'Readable'"
                message:
                  de: "Aus der GeoPackage-Datei liessen sich keine Modelle lesen."
                  en: "No models could be read from the GeoPackage file."
              - id: wrong-model
                expression: "!('ModelName' in [gpkg_metadata.Models])"
                message:
                  de: "Die Daten bilden nicht das Modell 'ModelName' ab."
                  en: "The data does not represent the 'ModelName' model."
        input:
          gpkgFile: "${step_output(file_matching.MatchedFiles)}"
        output_actions:
          - property: StatusMessage
            actions:
              - StatusMessage
      - id: gpkg_validation
        display_name:
          de: GPKG-Daten validieren
          en: Validating GPKG data
        process_id: gpkg_data_validator
        input:
          dbFile: "${step_output(file_matching.MatchedFiles)}"
          models: "${step_output(gpkg_metadata.Models)}"
```
