# Plugin System für Pipeline Prozessoren

Die grundsätzliche Funktionsweise der Pipeline ist in der "[Dokumentation und Konzepte im Zusammenhang mit geopilot und Pipelines](Pipelines.md)" beschrieben. Hier konzentrieren wir uns auf die funktionale Erweiterung durch Plugins.

## Einführung

Pipeline-Prozessoren sind zentrale Komponenten in den geopilot Pipelines, die von Verarbeitungsschritten durchgeführt werden, um Daten zu transformieren oder zu analysieren. Das Pipeline-Plugin-System ermöglicht es den Funktionsumfang der Pipelines zu erweitern indem solche Prozessoren dazugeladen werden können ohne die Codebasis von geopilot zu verändern oder zu erweitern.

Dies ermöglicht es den verschiedenen Kunden von geopilot spezifische Anforderungen zu erfüllen, indem sie massgeschneiderte Prozessoren entwickeln und in ihre Pipelines integrieren können.

Geopilot definiert von sich aus bereits eine Reihe von Standard-Pipeline-Prozessoren, die für gängige Anwendungsfälle geeignet sind und mit geopilot ausgeliefert werden. Diese sind beispielsweise:

- **XTF Matcher**: Filtert die hochgeladenen Dateien anhand konfigurierbarer Kriterien (Dateiendungen, ILI-Modelle, Dateinamen-Muster).
- **Interlis Validator**: Validiert Interlis Files auf inhaltliche Fehler.
- **ZIP Packetierer**: Packt Dateien in ein ZIP-Archiv.
- **XTF Fehlervisualisierung**: Erzeugt aus den Fehlern einer Interlis-Validierung eine Fehlervisualisierung (Kartenansicht und Fehlerbaum).

Aus dieser Menge von Standardprozessoren und Pluginprozessoren können die Pipelines gebaut werden. Die vollständige Liste der mitgelieferten Prozessoren, je mit Konfiguration, Inputs und Outputs, ist unter [Prozesse](Pipelines.md#prozesse) verlinkt.

## Dependency Management

Ein Pipeline-Prozessor wird in C# entwickelt. Dieser Prozessor muss das NuGet-Package [`GeoWerkstatt.Geopilot.PipelineCore`](../../src/Geopilot.PipelineCore/README.md) einbinden, um die notwendigen Definitionen nutzen zu können. Das Package wird unabhängig von der geopilot-Version versioniert (SemVer auf der öffentlichen Plugin-Oberfläche).

Beim Laden prüft geopilot, gegen welche PipelineCore-Version ein Plugin gebaut wurde, und lehnt inkompatible Plugins ab (das Plugin wird übersprungen, geopilot läuft weiter):

- Die **Major-Version** muss mit der von geopilot verwendeten übereinstimmen. Ein Major-Update von PipelineCore erfordert also einen Rebuild aller Plugins.
- Die **Minor-Version** des Plugins darf nicht höher sein als die von geopilot verwendete. Ein Plugin kann somit keine Funktionalität voraussetzen, welche die Zielumgebung noch nicht kennt.
- Ein Plugin, welches gegen eine ältere Minor- oder Patch-Version gebaut wurde, wird geladen; geopilot schreibt dazu eine Warnung ins Log.

Die PipelineCore-Bibliothek beinhaltet die folgenden Definitionen:

- **File**:
  - `IPipelineFileManager`: Interface zum Verwalten von Dateien innerhalb der Pipeline. Erzeugt `IPipelineFile`-Instanzen mit denen innerhalb der Prozessoren Dateien geschrieben, gelesen und zwischen den Schritten übertragen werden können. Dateien, welche über `IPipelineFileManager` erzeugt werden, werden automatisch von geopilot verwaltet und am Ende des Pipeline-Runs gelöscht. Bei Bedarf kann mit der Überladung `GeneratePipelineFile(originalRelativePath, originalFileName, fileExtension)` ein ursprünglicher relativer Pfad als Metadatum auf der erzeugten Datei mitgegeben werden (z.B. die Position einer Datei innerhalb eines entpackten ZIP-Archivs). Auf der Festplatte werden die Dateien immer flach im temporären Schritt-Verzeichnis abgelegt; der relative Pfad ist reines Metadatum.
  - `IPipelineFile`: Interface zum Übertragen von Dateien innerhalb der Pipeline. Stellt unter anderem die Eigenschaften `OriginalFileName`, `FileExtension` und `OriginalRelativePath` bereit. `OriginalRelativePath` ist Forward-Slash-getrennt, ohne führenden oder folgenden Trenner, ohne `..`-Segmente und ist ein leerer String für Dateien auf der obersten Ebene. Verwendung u.a. durch den ZIP Entpacker, um die ursprüngliche Verzeichnisstruktur eines Archivs auf den entpackten Dateien zu erhalten; nachgelagerte Prozessoren, welche eine flache Sicht benötigen, ignorieren die Eigenschaft.
- **Verarbeitung** (`PipelineProcessRun`-Annotation): Funktionen, welche die eigentliche Verarbeitung implementieren müssen mit dieser Annotation gekennzeichnet werden. Es muss genau eine Funktion mit dieser Annotation vorhanden sein, da sie die Hauptfunktion des Prozessors darstellt.
  - Rückgabewert: `Task<TResult>`, wobei `TResult` eine prozessspezifische Result-Klasse ist. Deren öffentliche Properties definieren die Resultate des Prozessors, welche folgenden Pipeline-Schritten zur Verfügung stehen. Alle öffentlichen, lesbaren Properties stehen dabei implizit zur Verfügung; sie müssen in der Pipeline-Definition nicht einzeln deklariert werden. Der Property-Name (PascalCase) ist der Name, unter dem ein Output referenziert wird; die Schreibweise hängt vom Kontext ab: In Step-Inputs ist es die Referenz `${step_output(stepId.PropertyName)}`, in den NCalc-Expressions von Conditions und Delivery-Restrictions der Parameter `[stepId.PropertyName]` bzw. `{stepId.PropertyName}` (siehe [Pipelines.md](Pipelines.md)). Die Engine liest die Property per Reflection.
  - Parameter: Variabel werden anhand des Parameter-Namens und Typs der Run-Methode übergeben. Das Mapping hierzu wird über die Pipeline-Definition bestimmt. Als letzter Parameter kann ein optionales `CancellationToken` übergeben werden, um die Möglichkeit zu bieten, die Verarbeitung vorzeitig zu beenden.
  - Input-Quelle: Woher ein Parameter seinen Wert erhält (Output eines Vorschritts, die hochgeladenen Dateien via `${upload()}`, eine Ressourcendatei via `${file()}` oder ein Literal), wird in der Pipeline-Definition über das `input`-Mapping festgelegt (siehe [Pipelines.md](Pipelines.md)). Aus Sicht des Prozessors ist das jeweils ein gewöhnlicher Parameter: Eine Datei-Sammlung nimmt man als `IPipelineFile[]` entgegen (aus jeder Quelle befüllbar, auch `${upload()}`), ohne besondere Kennzeichnung.
- **Dateien-Sammlung**:
  - Datei-Sammlung: Eine Sammlung von `IPipelineFile` ist ein `IPipelineFile[]` (an Kontext-Flächen wie dem Upload auch `IReadOnlyList<IPipelineFile>`). Ein Run-Parameter für mehrere Dateien ist ein `IPipelineFile[]`, aus jeder Quelle befüllbar.
  - Datei-Filter stehen als Extension-Methods auf `IEnumerable<IPipelineFile>` zur Verfügung (also auf `IPipelineFile[]` wie auf jeder Datei-Sequenz) und können verkettet werden:
    - `WithExtensions(HashSet<string> extensions)`: Filtert nach Dateiendungen (ohne Punkt, z.B. `xtf`). Vergleich ist case-insensitive.
    - `WithMatchingName(string namePattern)`: Filtert nach einem Regex-Muster gegen den originalen Dateinamen.

## Anforderungen an Pipeline-Prozessoren

- **Instanzierung**: Es muss ein Konstruktor vorhanden sein, welcher die Initialisierung des Prozessors ermöglicht. Dieser Konstruktor kann 0-n Parameter besitzen, welche in der Pipeline-Definition angegeben werden können. Es muss jedoch sichergestellt werden, dass die Pflichtparameter durch die Konfiguration eindeutig identifizierbar sind, damit sie korrekt zugeordnet werden können. Optionale und Parameter nichtidentifizierbare Parameter werden mit `null` initialisiert. Die Parameter sind variabel und können die folgenden Typen aufnehmen:
  - `ILogger`: Logger-Instanz für das Logging innerhalb des Prozessors.
  - `IPipelineFileManager`: Instanz zum Verwalten von Dateien innerhalb der Pipeline.
  - `IIli2GpkgClient`: Client für ili2gpkg-Operationen (Schema-Import, Daten-Import, Export) gegen den ilitools-wrapper-Service. Die Adresse des Services wird von geopilot konfiguriert; das Plugin benötigt dazu keine eigene Konfiguration.
  - `int` und `int?`: Ganzzahlige Werte, welche in der Pipeline-Definition als Konfiguration angegeben werden können.
  - `double` und `double?`: Gleitkommazahlen, welche in der Pipeline-Definition als Konfiguration angegeben werden können.
  - `string` und `string?`: Zeichenfolgen, welche in der Pipeline-Definition als Konfiguration angegeben werden können.
  - `bool` und `bool?`: Boolesche Werte, welche in der Pipeline-Definition als Konfiguration angegeben werden können.
  - `HashSet<string>` und `HashSet<string>?`: Mengen von Zeichenfolgen, welche in der Pipeline-Definition als Liste konfiguriert werden können (z.B. für Dateiendungen oder Muster).
  - Weitere Typen sind möglich, sofern sich der konfigurierte Wert in den Parametertyp konvertieren lässt (z.B. `TimeSpan`, Enums oder Listen wie `IReadOnlyList<string>`). Listen können allerdings nur in der Pipeline-Definition (`default_config`) angegeben werden, nicht in den Appsettings unter `Pipeline:ProcessConfigs`; diese Ebene trägt nur skalare Werte.
- **Verarbeitung**: Die Verarbeitung der Daten erfolgt in der `PipelineProcessRun` Methode, welche die Hauptverarbeitungslogik des Prozessors enthält.
- **Ressourcenmanagement** (optional): Wenn der Prozessor Ressourcen verwendet, die freigegeben werden müssen (z.B. Datenbankverbindungen, Dateihandles, etc.), sollte er das `IDisposable` Interface implementieren und die entsprechenden Aufräumarbeiten in der `Dispose` Methode durchführen.

## Beispiel eines Pipeline-Prozessors

```csharp
public class MyProcess : IDisposable
{
    private ILogger logger;

    public MyProcess(string someStringParameter, int someIntParameter, HashSet<string>? someFilterList, IPipelineFileManager pipelineFileManager, ILogger logger)
    {
        // Initialize the processor with the provided configuration and parameters
        this.logger = logger;
    }

    public void Dispose()
    {
        // Cleanup resources if necessary
    }

    [PipelineProcessRun]
    public Task<MyProcessResult> PipelineProcessRun(IPipelineFile[] files, string someRandomStringInput, int[] someRandomIntInput, CancellationToken cancellationToken)
    {
        // files ist ein gewöhnlicher Input-Parameter; die Quelle (hier ${upload()}) wird in der Pipeline-Definition festgelegt
        // Die Dateien können gefiltert werden (Extension-Methods auf IEnumerable<IPipelineFile>):
        // var filtered = files.WithExtensions(new HashSet<string> { "xtf" }).ToArray();

        logger.LogInformation($"run process <MyProcess>.");
        return Task.FromResult(new MyProcessResult
        {
            OutputA = "some output A",
            OutputB = "some output B",
        });
    }
}

// Result-Klasse des Prozessors: Jede öffentliche Property ist ein Output, der in der
// Pipeline-Definition implizit über den Property-Namen (PascalCase) referenziert wird.
public class MyProcessResult
{
    public required string OutputA { get; set; }

    public required string OutputB { get; set; }
}

```

## Lokalisierte Status-Nachrichten

Ein Prozessor kann eine lokalisierte Status-Nachricht zurückgeben, die in der Benutzeroberfläche angezeigt wird. Dazu wird eine Result-Property über `output_actions` mit der Aktion `StatusMessage` (siehe [Pipelines](Pipelines.md)) getaggt. Der Wert eines solchen Outputs ist vom Typ `LocalizedText` (ab PipelineCore 1.3), einem unveränderlichen lokalisierten Text mit dem Sprachcode als Key:

```csharp
// Property auf der Result-Klasse des Prozessors:
public required LocalizedText StatusMessage { get; set; }

// ... beim Erzeugen des Results gesetzt:
StatusMessage = new LocalizedText(new Dictionary<string, string>
{
    { "de", "Verarbeitung erfolgreich abgeschlossen." },
    { "en", "Processing completed successfully." },
    { "fr", "Traitement terminé avec succès." },
    { "it", "Elaborazione completata con successo." },
}),
```

Ein `Dictionary<string, string>` (Key = Sprachcode) wird aus Gründen der Abwärtskompatibilität weiterhin akzeptiert, sodass bestehende Prozessoren ohne Anpassung weiterlaufen.

## Einbinden des Plugins in geopilot

Die Appsettings von geopilot bieten die Möglichkeit, die zu ladenden Pipeline-Prozessor-Plugins zu definieren. Hierzu werden die Namen der Assemblies angegeben, welche die Prozessoren enthalten. Die Plugins werden im Abschnitt `Pipeline:Plugins` definiert. Hier erwarten wir eine Liste von Strings, welche die Namen der zu ladenden Assemblies enthalten.

- `Pipeline:Definition`: Definiert den Pfad zur Pipeline-Definition, welche die Struktur der Pipelines und die Konfiguration der einzelnen Schritte beschreibt. Dieser Pfad kann absolut oder relativ sein.
- `Pipeline:Plugins`: Absolute oder Relative Pfade zu den Assemblies, welche die Pipeline-Prozessoren enthalten. Es können mehrere Plugins angegeben werden, welche dann alle geladen werden. Neben der JSON-Liste ist auch ein einzelner, kommaseparierter Wert möglich, damit die Plugins als flache Umgebungsvariable gesetzt werden können (`Pipeline__Plugins=a.dll,b.dll`).
- `Pipeline:ProcessConfigs`: Hier können spezifische Konfigurationen für die einzelnen Prozessoren angegeben werden. Der Key ist der vollständige Name der Prozessklasse (inklusive Namespace). Der Value ist ein Dictionary von Schlüssel-Wert-Paaren, welche die Basis-Konfiguration für diesen Prozessor darstellen. Diese Konfigurationen werden den Prozessor-Initialisierungsfunktionen mittels Parameter übergeben und können in der Pipeline-Definition nicht überschrieben werden. Es sind nur skalare Werte möglich; listenwertige Parameter gehören in die `default_config` der Pipeline-Definition.

Ein typisches Beispiel für die Konfiguration könnte wie folgt aussehen:

```json
{
  "Pipeline": {
    "Definition": "path/to/myPipeline.yaml",
    "Plugins": [
      "directory/myProcessorPluginA.dll"
    ],
    "ProcessConfigs": {
      "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess": {
        "checkServiceBaseUrl": "http://localhost:3080/"
      }
    }
  }
}
```
