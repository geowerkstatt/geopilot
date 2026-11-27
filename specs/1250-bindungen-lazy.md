# Pipeline-Bindungen erst im Pipeline-Schritt lesen (#1250)

## Begriffe

- **Bindung:** Was geopilot einem Prozessor neben Konfiguration und Eingaben zur Verfügung stellt, etwa die Zuordnung der Referenzdaten aus dem Admin-Portal. Der Prozessor nimmt sie im Konstruktor entgegen. Gelesen wird sie erst, wenn ein Schritt sie abruft, und danach bekommt jeder Schritt der Verarbeitung denselben Stand. Was ein Schritt abgerufen hat, steht im Protokoll als seine Eingabe mit Herkunft `Binding`. Nicht zu verwechseln mit dem Binden der Eingaben eines Schritts (`InputBinder`). Im Code: `PipelineJobBindings`, etwa `IRefMappings`.
  _Übersetzungen_: EN binding, FR une liaison, IT un'associazione

## Umsetzung

**Zwei Ebenen.** Bei eingeschaltetem `RefMapping:Enabled` erzeugt `JobLifecycle.StartAsync` pro Verarbeitung eine Quelle, ohne zu lesen. Sie hält ein `Lazy<Task<...>>`, das beim ersten Zugriff über eine `IServiceScopeFactory` in einem eigenen Scope alle Einträge liest, wie heute `ReadJobBindingsAsync`. Jeder weitere Zugriff in der Verarbeitung bekommt dasselbe Ergebnis, auch aus einem anderen Schritt. `PipelineJobBindings` hält statt fertiger Objekte pro Bindungstyp eine Fabrik, und `PipelineProcessFactory` erzeugt damit für jeden Konstruktor-Parameter eines Bindungstyps eine eigene Instanz, die Verarbeitung, Schritt-Id und Parametername kennt. Den Cache hält die Quelle, nicht die Instanz. Ohne Schalter bleibt der Parameter `null`.

**Interface.** `IRefMappings.GetEntriesAsync(CancellationToken)` ersetzt `Entries`. Die Instanz eines Schritts holt die Einträge bei der Quelle und schreibt beim ersten Aufruf die Eingabe ihres Schritts ins Protokoll; weitere Aufrufe im selben Schritt schreiben nichts mehr. `XtfValidatorProcess` ruft `GetEntriesAsync` wie heute `Entries` nur mit `refMappingFromPortal: true` auf, nach den Prüfungen in `RunAsync`, die unverändert bleiben. Das Interface bleibt intern.

**Lesefehler.** Scheitert das Lesen, wirft `GetEntriesAsync`, und der Schritt scheitert mit dem Fehler an seiner Zeile, ohne Wiederholung. Das ist ein technischer Fehler wie jede andere Exception eines Prozessors. Der Start liest nichts mehr und kann daran nicht mehr scheitern.

**Protokoll.** Die Eingabe bekommt die neue Herkunft `InputOrigin.Binding`. `Parameter` ist der Name des Konstruktor-Parameters (`refMappings`), `Reference` der Schlüssel der Bindung (`refMapping`), die Art `Value`. `Value` hält alle Einträge als JSON mit `scope`, `refDataUris` und `comment`, wie bisher in `Recordings.Bindings`, ungekürzt und bei leerer Zuordnung `[]`. Die Grenze von 500 Zeichen gilt nur für Werte, die die Laufzeit rendert. `InputOrigin` ist im Paket `GeoWerkstatt.Geopilot.Pipeline` öffentlich, `Binding` kommt additiv dazu.

Geschrieben wird über `IRecorder` in einem eigenen Scope, mit einer neuen Methode, die der Zeile des Schritts eine Eingabe anhängt. Die Zeile legt schon `RecordJobStartedAsync` an. Anders als die übrigen Methoden für Schritte legt die neue eine fehlende Zeile nicht nach, weil die Instanz kein `IPipelineStep` hat; sie protokolliert das und schreibt nichts. `GetEntriesAsync` wartet das Schreiben ab, bevor es die Einträge zurückgibt. Das Schreiben ist weich wie die übrigen: Scheitert es, läuft der Schritt weiter und sieht im Protokoll aus wie einer, der nicht gelesen hat. Scheitert das Lesen, entsteht keine Eingabe. Weil die Eingaben nach ihrer Id sortiert sind, steht die Bindung nach den übrigen Eingaben des Schritts.

`Recordings.Bindings` fällt weg, mit ihr der Parameter `bindings` von `RecordJobStartedAsync`. Die Migration löscht die Spalte, ohne die Abbilder zu übernehmen. `Origin` wird als Text gespeichert und braucht keine Migration.

**Rückbau.** Weg fallen `GetUsedBindingTypes` in `IPipelineFactory` und `PipelineFactory`, `IPipelineProcessBuilder.GetBindingTypes`, `JobLifecycle.ReadJobBindingsAsync` und `PipelineJobBindings.ToSnapshotJson`. Das `sealed` an `PipelineFactory` kam mit `GetUsedBindingTypes` und fällt mit ihm.

**GUI.** `recordingSteps.tsx` bekommt die Beschriftung `inputOriginBinding`: DE «Bindung», EN «Binding», FR «Liaison», IT «Associazione». Bei einer Eingabe dieser Herkunft steckt der Wert in einer `Box` mit `maxHeight` und `overflow: auto`, weil Tabellenzellen `max-height` ignorieren. Die Box ist mit `tabIndex={0}` per Tastatur erreichbar, damit sie sich ohne Maus scrollen lässt. Die Typen werden regeneriert.

**Doku.**

- `docs/Ausfuehrungsprotokoll.md`: Die Aufzählung beim Start nennt keine Bindungen mehr. Die Eingaben eines Schritts sind nicht mehr nur, was sein Prozessor beim Start bekam, sondern auch, was er im Lauf aus einer Bindung abrief, ungekürzt und nach den übrigen. Der Abschnitt «Die Bindungen» beschreibt diese Eingabe: wann sie entsteht, was `Value` hält, `[]` bei leerer Zuordnung. Die Liste der Herkünfte bekommt `Binding`, die Beispielabfrage geht über `RecordingInputs` mit `"Value"::jsonb`.
- `docs/pipeline/Prozessoren/xtf-validierung.md` und `docs/pipeline/Pipelines.md`: Statt «beim Start des Jobs» liest geopilot die Einträge beim ersten Abruf in der Verarbeitung; eine Änderung im Portal davor erreicht die Verarbeitung noch. `docs/Referenzdaten.md`: Der Verweis aufs Protokoll nennt den lesenden Schritt.
- Die Doc-Kommentare an `PipelineJobBindings`, `IRefMappings`, `StepInput`, `RecordingInput`, `RecordingResponse` und `JobLifecycle.StartAsync` beschreiben das neue Verhalten.
- `GLOSSARY.md`: der Eintrag aus *Begriffe*.
- CHANGELOG: kein neuer Eintrag, denn die Zuordnung im Admin-Portal ist unveröffentlicht. Ihr Eintrag unter Unreleased sagt statt «as they were when its job started» und «the new column `Bindings`», dass ein Schritt die Einträge so bekommt, wie die Verarbeitung sie zuerst las, und dass das Protokoll sie als Eingabe des lesenden Schritts mit Herkunft `Binding` festhält.

## Prüfung

- Pipeline-Tests: Ein Schritt mit `refMappingFromPortal: true` ruft `GetEntriesAsync` genau einmal auf. Ein Schritt ohne Opt-in und ein übersprungener Schritt rufen es nie auf; das zeigt eine Attrappe, die die Aufrufe zählt. Die Tests zu `GetUsedBindingTypes` entfallen.
- Api-Tests zur Implementation im Host: Das Erzeugen liest nichts. Zwei Schritte bekommen denselben Stand, auch wenn sich ein Eintrag zwischen ihren Aufrufen ändert. Pro Schritt entsteht genau eine Eingabe mit Herkunft `Binding`, ungekürzt. Ohne Schalter gibt es keine Bindung. Mit einer Attrappe der Quelle: Eine leere Zuordnung ergibt `[]`, und wirft die Quelle, scheitert der Schritt mit dem Fehler an seiner Zeile, ohne Eingabe. Die leere Zuordnung nicht gegen die gemeinsame Testdatenbank prüfen, weil andere Tests dort Einträge anlegen. `RecorderTest`: Die neue Methode hängt die Eingabe an die Zeile des Schritts.
- Der Migrationstest läuft mit, `Recordings.Bindings` ist weg.
- `recordingDetail.cy.ts`: Eine Eingabe mit Herkunft Bindung zeigt die Beschriftung, und ihre Zelle ist in der Höhe begrenzt.
- Smoke-Test in der DMAV-Testwelt:
  1. Eine DMAV-Lieferung mit `refMappingFromPortal: true` lädt die Referenzdaten wie bisher, sichtbar im Log des Werkzeugs. Die Protokollseite zeigt beim Validierungsschritt die Eingabe «Bindung» mit dem Eintrag für 449.
  2. Dieselbe Pipeline, lokal mit einer Skip-Condition, die den Validierungsschritt überspringt: Im Protokoll steht keine Eingabe «Bindung».
  3. Mit `MaxConcurrentJobs` 1 zwei DMAV-Lieferungen starten und, solange die zweite wartet, einen Eintrag im Portal ändern: Die zweite Verarbeitung zeigt den geänderten Stand, obwohl sie vor der Änderung gestartet wurde.
- CI ist grün, auch der Abgleich der generierten Typen.
