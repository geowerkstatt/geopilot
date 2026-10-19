# Begriffe

Die Begriffe rund um den Lebenszyklus eines Verarbeitungsjobs, so wie sie in Issues, Reviews und im Code verwendet werden. Heisst ein Begriff im Code anders, steht der Name dort dabei. Ändert sich ein Begriff oder kommt einer dazu, wird diese Datei im selben PR nachgeführt.

## Job

Alles, was geopilot zur Verarbeitung eines Uploads mit einem Mandat im Speicher hält: Zustand, Pipeline und den Verweis auf den Upload. Ein Job entsteht beim Start und lebt bis zu seinem Retirement. Pro Upload ist höchstens ein Job aktiv, also Pending oder Running; ein zweiter Start wird abgelehnt, bis dieser Job endet. Scheitert der Start, bleibt kein Job zurück. Im Code: `ProcessingJob`, gestartet in `JobLifecycle.StartAsync`.

## Lauf

Was das Ausführungsprotokoll zu einem Job festhält, vom Start über Preflight und die Schritte der Pipeline bis zum Ausgang. Heute gehört zu jedem Job genau ein Lauf. Anders als der Job überlebt der Lauf das Retirement und Neustarts, siehe [Ausführungsprotokoll](docs/Ausfuehrungsprotokoll.md). Im Code: `PipelineRun`.

## Ausgang

Wie die Pipeline eines Laufs geendet hat, gemeldet vom Runner: zu Ende gelaufen mit dem Zustand, den die Pipeline meldet, Zeitüberschreitung, Herunterfahren oder Absturz. Lässt eine Bedingung die Daten scheitern, läuft die Pipeline mit `Failed` zu Ende. Ein Absturz ist dagegen eine Exception, die den Lauf abbricht: Ein Prozessor wirft, oder das Speichern der Downloads oder das Stagen der Lieferdateien scheitert. Im Code: `RunOutcome`.

## Folgen

Was aus einem Ausgang für den Job folgt, soweit es vom Ausgang abhängt: sein neuer Zustand und ob der Upload freigegeben wird. Was bei jedem Ausgang geschieht, etwa die Pipeline entsorgen oder das Protokoll schreiben, gehört nicht dazu. Im Code: `JobDecision`, entschieden in `JobLifecycle.Decide`.

## Freigabe

Das Löschen der hochgeladenen Dateien eines Jobs aus dem Upload-Speicher. Ob sie am Laufende geschieht, entscheidet `JobLifecycle.Decide`. Ein Upload, der nicht freigegeben wird, bleibt für einen erneuten Start liegen, bis seine Session abläuft (`Upload:CleanupAgeHours`). Im Code: `IUploadOrchestrationService.ReleaseUploadAsync`.

## Retirement

Das Entfernen eines Jobs, wenn seit dem Ende seines Laufs `Processing:JobRetention` vergangen ist: der Eintrag im Speicher sowie seine Downloads, Visualisierungen, Arbeits- und Staging-Verzeichnisse. Deklarierte Lieferungen und der Lauf im Protokoll bleiben erhalten. Den Upload gibt das Retirement nicht frei, der ist dann schon freigegeben oder wird bis zum Ablauf seiner Session behalten. Im Code: `ProcessingJobCleanupService`, für den Job selbst `JobLifecycle.Retire`.

## Versuch

Eine maschinelle Anlieferung aus Sicht des Aufrufers: Mandat, Lieferangaben und Daten, die er übergibt, bis die Lieferung deklariert oder abgelehnt ist. Ein Versuch lebt im Speicher und höchstens so lange wie sein Job, siehe [Maschinelle Anlieferung](docs/MaschinelleAnlieferung.md). Im Code: `Submission`.

## Lieferabsicht

Der beim Start geäusserte Wunsch, dass geopilot die Lieferung selbst deklariert, sobald der Lauf lieferbar endet. Heute hat nur ein Versuch eine Lieferabsicht. Sie steckt in der `Submission`, als Declarer und Lieferangaben.
