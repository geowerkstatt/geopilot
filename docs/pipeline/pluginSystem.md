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
  - `IPipelineFileManager`: Interface zum Verwalten von Dateien innerhalb der Pipeline. Erzeugt `IPipelineFile`-Instanzen mit denen innerhalb der Prozessoren Dateien geschrieben, gelesen und zwischen den Schritten übertragen werden können. Dateien, welche über `IPipelineFileManager` erzeugt werden, werden automatisch von geopilot verwaltet und am Ende des Pipeline-Runs gelöscht. Bei Bedarf kann mit der Überladung `GeneratePipelineFile(originalRelativePath, originalFileName, fileExtension)` ein ursprünglicher relativer Pfad als Metadatum auf der erzeugten Datei mitgegeben werden (z.B. die Position einer Datei innerhalb eines entpackten ZIP-Archivs). Auf der Festplatte werden die Dateien immer flach im temporären Schritt-Verzeichnis abgelegt; der relative Pfad ist reines Metadatum. `CreateWritableCopyAsync(source, name, CancellationToken)` erstellt eine eigene, beschreibbare Kopie einer fremden Datei.
  - `IPipelineFile`: Interface zum Übertragen von Dateien innerhalb der Pipeline. Stellt unter anderem die Eigenschaften `OriginalFileName`, `FileExtension` und `OriginalRelativePath` bereit. `OriginalRelativePath` ist Forward-Slash-getrennt, ohne führenden oder folgenden Trenner, ohne `..`-Segmente und ist ein leerer String für Dateien auf der obersten Ebene. Verwendung u.a. durch den ZIP Entpacker, um die ursprüngliche Verzeichnisstruktur eines Archivs auf den entpackten Dateien zu erhalten; nachgelagerte Prozessoren, welche eine flache Sicht benötigen, ignorieren die Eigenschaft.
  - Auf den Inhalt wird asynchron zugegriffen, weil eine Datei erst beim ersten Zugriff geholt werden kann (hochgeladene Dateien liegen bis dahin im Object Storage). Beide Methoden nehmen ein optionales `CancellationToken` entgegen; reiche den Token der Run-Methode weiter, damit ein abgebrochener Job das Holen der Datei abbricht.
    - `Task<Stream> OpenReadAsync(CancellationToken)`: Liest den Inhalt. Der zurückgegebene Stream ist seekable und gehört dem Aufrufer (`using`).
    - `Task<string> GetLocalPathAsync(CancellationToken)`: Liefert den Pfad einer eigenen Kopie, um die Datei an externe Werkzeuge zu übergeben, die auf Pfaden statt Streams arbeiten (z.B. SQLite). Die Kopie darf verändert werden; das Original des liefernden Schrittes bleibt unberührt.
    - `FileStream OpenWriteFileStream()`: Nur für Dateien, die der eigene Schritt über `IPipelineFileManager` erzeugt hat. Auf einer Eingabedatei wirft die Methode.
  - Eine Datei wird höchstens einmal geholt; weitere Zugriffe verwenden die lokale Kopie. Wer nur Metadaten liest (Name, Endung, relativer Pfad), löst keinen Transfer aus. Ein Matcher, der nach Dateiendung filtert, lässt die aussortierten Dateien während des Laufs also gar nicht übertragen. (Unabhängig davon archiviert eine Lieferung am Ende jede hochgeladene Datei, das liegt aber ausserhalb des Pipeline-Laufs.)
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
  - `IIlivalidatorClient`: Client für Validierungen mit ilivalidator gegen denselben Service, ebenfalls ohne eigene Konfiguration.
  - `IPipelineFile` und `IPipelineFile?`: Eine Datei, die das Deployment mitbringt. Konfiguriert wird ihr **Pfad relativ zum Ressourcen-Verzeichnis** (Appsettings `Storage:ResourcesDirectory`), also derselben Wurzel, gegen die eine `${file(...)}`-Referenz im `input` auflöst. Der Pfad muss innerhalb dieser Wurzel liegen und eine existierende Datei nennen, sonst startet die Applikation nicht. Damit gehören konstante Dateien wie eine Vorlage, eine Nachschlagetabelle oder ein Modell-Repository in die Konfiguration eines Prozessors, statt in jedem Schritt als Input verdrahtet zu werden, und lassen sich in der Basis-Konfiguration unveränderbar festlegen.
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

## Visualisierungen sind eingebauten Prozessoren vorbehalten

Die Ausgabeaktion `Visualization` (siehe [Pipelines](Pipelines.md)) steht Plugins **nicht** zur Verfügung. Ein Plugin-Prozessor kann keine Visualisierung liefern.

Der Grund liegt darin, dass eine Visualisierung aus zwei Hälften besteht. Die eine ist der Envelope, den der Prozessor zurückgibt; sein Markierungs-Typ `IVisualization` ist bewusst nicht Teil des Plugin-Vertrags in `GeoWerkstatt.Geopilot.PipelineCore`. Die andere ist die Komponente, die den Envelope darstellt, und die liegt im geopilot-Frontend: welche Komponente für welchen `type` zuständig ist, steht dort zur Übersetzungszeit fest. Ein Plugin könnte also eine Nutzlast erzeugen, für die es keine Darstellung gibt.

Verwendet ein Prozessor die Ausgabeaktion trotzdem, schlägt der Schritt zur Laufzeit fehl.

Wird eine Visualisierung für einen fachlichen Anwendungsfall gebraucht, ist der Weg eine Absprache mit der geowerkstatt: sie entsteht dann als eingebauter Visualisierungstyp und steht danach allen Installationen zur Verfügung.

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
      "Geopilot.Pipeline.Processes.XtfErrorVisualization.XtfErrorVisualizationProcess": {
        "baseMapWmtsCapabilitiesUrl": "https://wmts.example.com/1.0.0/WMTSCapabilities.xml"
      }
    }
  }
}
```

## Integrationstests im Plugin-Repository

Ein Prozessor wird vom Framework auf zwei Wegen versorgt, und beide sind Reflection über eine Pipeline-Definition: die **Konstruktor-Injection** löst die Parameter aus der zusammengeführten Konfiguration auf, die **Run-Methoden-Injection** bindet die Parameter der `[PipelineProcessRun]`-Methode an die `input`-Einträge des Schritts. Beides lässt sich nicht sinnvoll nachbauen, ohne die Runtime nachzubauen.

**Ein Integrationstest führt einen Prozessor deshalb immer über eine Pipeline aus.** Die Pipeline-Klassen selbst sind nicht Teil der öffentlichen API; ein Schritt kann nicht von Hand zusammengesteckt werden. Der Einstieg ist `PipelineFactory`, gespeist aus einer Definition. Was ein Test damit prüft, ist genau das, was in Produktion schiefgehen kann: ein Konstruktor, der nicht zur Konfiguration passt, und eine Run-Methode, deren Parameter nicht zu den `input`-Einträgen passen.

Dazu referenziert das Testprojekt zusätzlich das NuGet-Paket `GeoWerkstatt.Geopilot.Pipeline` und baut die Pipeline aus der eigenen Definitionsdatei:

```csharp
var processFactory = new PipelineProcessFactory(
    Options.Create(new PipelineOptions { Plugins = [pathToPluginDll] }),
    Options.Create(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" }),
    NullLoggerFactory.Instance);

var factory = PipelineFactory.Builder()
    .File("Pipelines/myPipeline.yaml")
    .PipelineProcessFactory(processFactory)
    .LoggerFactory(NullLoggerFactory.Instance)
    .PipelineTempDirectory(workingDirectory)
    .ResourcesDirectory(resourcesDirectory)
    .Build();

using var pipeline = factory.CreatePipeline("my_pipeline", Guid.NewGuid());
var context = await pipeline.Run(uploadFiles, CancellationToken.None);
```

Damit laufen die Konstruktor-Injection, die Auflösung der `${...}`-Ausdrücke und die in geopilot eingebauten Prozessoren so, wie sie es zur Laufzeit tun. Ein einzelner Schritt lässt sich über `pipeline.Steps` herausgreifen und mit `step.Run(context, ct)` isoliert ausführen; die Ergebnisse vorangehender Schritte werden dann über `PipelineContext.StepResults` gestellt.

Drei Punkte, die dabei regelmässig überraschen:

**Es werden immer alle Schritte konstruiert**, auch wenn nur einer ausgeführt wird. Die Konfigurationsschicht muss deshalb jeden Prozessor der Definition befriedigen, nicht nur den getesteten. Parameter, die nicht aus der `default_config` der Definition stammen, kommen in Produktion aus `Pipeline:ProcessConfigs` und müssen im Test entsprechend gesetzt werden.

Wer einen einzelnen Prozessor isoliert testen will, gibt der Factory statt der produktiven Datei eine minimale Definition als Text mit. `Builder().Yaml(...)` nimmt die Definition direkt entgegen, und es wird nur konstruiert, was darin steht:

```csharp
var factory = PipelineFactory.Builder()
    .Yaml("""
        processes:
          - id: only_this
            implementation: MyPlugin.Processors.MyProcess
            default_config:
              someParameter: "value"
        pipelines:
          - id: isolated
            display_name:
              en: Isolated
            steps:
              - id: the_step
                display_name:
                  en: The step
                process_id: only_this
                input:
                  someInput: "${step_output(upstream.SomeOutput)}"
        """)
    // ... übrige Builder-Aufrufe wie oben
    .Build();
```

Ein Test gegen die produktive Definition prüft die Verdrahtung, wie sie beim Kunden läuft; ein Test gegen eine Minimaldefinition prüft einen Prozessor für sich. Beides ist sinnvoll, die Wahl hängt vom Szenario ab.

Hat der zu testende Schritt höchstens einen Datei-Input, lässt er sich aus dem Upload speisen (`someInput: "${upload()}"`) und über `pipeline.Run(uploadFiles, ct)` ausführen. Dann muss gar kein Ergebnis eines Vorgängerschritts gestellt werden. Bei mehreren Datei-Inputs geht das nicht, weil `${upload()}` allen verdrahteten Parametern dieselbe Liste reicht; dort führt `${file(...)}` mit einem auf die Testfixtures gesetzten `ResourcesDirectory` zum selben Ziel.

Ob ein `${step_output(...)}` auf eine existierende und typkompatible Eigenschaft des echten Result-Typs zeigt, lässt sich ohne Ausführung prüfen. `IPipelineFactory.ValidateDefinition()` beantwortet das statisch für die ganze Definition, also auch für Schritte, die der Test nicht mitlaufen lässt. Als Preflight vor dem Lauf:

```csharp
var validation = factory.ValidateDefinition();
Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
```

Die Meldung nennt Pipeline, Schritt und Prozess und ist dieselbe, mit der der Server den Start verweigert. Ein solcher Test schlägt also dort fehl, wo auch das Deployment scheitern würde, und zwar bevor irgendein Prozessor läuft.

Zu beachten bleibt: der Preflight prüft die Verdrahtung, nicht das Laufzeitverhalten. Wer ausschliessen will, dass ein erzeugender Schritt etwas anderes liefert als sein Result-Typ verspricht, muss die beteiligten Schritte gemeinsam ausführen.

**Prozessoren werden in einen eigenen `AssemblyLoadContext` geladen.** Der Typ einer Prozessor-Instanz ist deshalb *nicht identisch* mit dem direkt referenzierten Typ, obwohl Typname und DLL-Pfad übereinstimmen. `is`-Prüfungen und Casts schlagen fehl:

```csharp
step.Process is MyProcess               // false
step.Process.GetType() == typeof(MyProcess)  // false
```

Auf Ergebnisse wird darum über den Namen zugegriffen, nicht über einen Cast:

```csharp
var value = stepResult.ExtractProperty(nameof(MyProcessResult.MyOutput));
```

**Externe Abhängigkeiten werden nach der Konstruktion ersetzt.** Die Runtime erzeugt die Prozessoren selbst und bietet dafür keine vorgesehene Naht. Ein Test, der einen `HttpClient` oder einen `IIli2GpkgClient` durch ein Test-Double ersetzen will, greift über `IPipelineStep.Process` per Reflection auf das private Feld zu. Aus dem vorigen Punkt folgt, dass das über den Laufzeittyp geschehen muss:

```csharp
step.Process.GetType()
    .GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)!
    .SetValue(step.Process, testHttpClient);
```

Das ist bewusst als Behelf dokumentiert und kein stabiler Vertrag. Ob dafür eine gestaltete Schnittstelle entsteht, wird in [Issue #665](https://github.com/geowerkstatt/geopilot/issues/665) evaluiert.
