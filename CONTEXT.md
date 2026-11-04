# Begriffe

Die Begriffe rund um den Lebenszyklus eines Verarbeitungsjobs und seiner Lieferung, so wie sie in Issues, Reviews und im Code verwendet werden. Heisst ein Begriff im Code anders, steht der Name dort dabei. Ändert sich ein Begriff oder kommt einer dazu, wird diese Datei im selben PR nachgeführt.

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

## Deklarierbar

Ob aus einem Lauf je eine Lieferung werden kann: Wer ihn startet, ist Mitglied einer Organisation des Mandats, und das Mandat nimmt Lieferungen an. Dieselbe Regel entscheidet `allowDelivery` in der Mandatsübersicht; ein öffentliches Mandat öffnet sie Nichtmitgliedern nicht. Entschieden wird einmal beim Start. Einen Lauf, den niemand deklarieren kann, staged der Runner nicht, und seine Deklaration wird abgelehnt, wer auch immer sie verlangt. Im Code: `ProcessingJob.IsDeclarable`, entschieden in `MandateService.IsDeclarableAsync`.

## Versuch

Eine maschinelle Anlieferung aus Sicht des Aufrufers: Mandat, Lieferangaben und Daten, die er übergibt, bis die Lieferung deklariert oder abgelehnt ist. Ein Versuch lebt im Speicher und höchstens so lange wie sein Job, siehe [Maschinelle Anlieferung](docs/MaschinelleAnlieferung.md). Im Code: ein `ProcessingJob` mit `DeliveryIntent`, abgefragt über den `SubmissionController`.

## Lieferabsicht

Der beim Start geäusserte Wunsch, dass geopilot die Lieferung selbst deklariert, sobald der Lauf lieferbar endet. Heute hat nur ein Versuch eine Lieferabsicht. Sie wird beim Anlegen des Jobs an ihn gebunden, mit Lieferant und Lieferangaben, sodass kein Lauf enden kann, bevor sie bekannt ist. Scheitert die Deklaration, steht der Grund auf dem Job; gelingt sie, ist die Lieferung selbst der Beleg. Im Code: `ProcessingJob.DeliveryIntent`, deklariert in `JobLifecycle.RunFinishedAsync`.

## Löschfenster

Die Zeit nach der Deklaration, in der ein Lieferant seine Lieferung noch selbst löschen darf (`Delivery:UploaderDeleteEnabled`). Es schliesst nach `Delivery:DeleteDuration` oder beim nächsten Zeitpunkt aus `Delivery:DeleteRestrictInterval`, je nachdem, was früher kommt; das Intervall gilt in der Zeitzone des Servers. Ohne beide Grenzen schliesst es nie. Admins löschen unabhängig davon. Im Code: `DeliveryOptions.GetDeleteWindowEnd`.

## Reaktion

Was auf eine Lieferung folgt, sobald sie nicht mehr zurückgezogen werden kann (ADR 0015). Ein Lieferungs-Schritt gibt sie weiter: Sind alle ihre Lieferungs-Schritte `Done` oder hat sie keine, ist sie weitergegeben, und erst dann zeigt sie der STAC-Katalog. Eine Benachrichtigung, die begleitende Reaktion, berichtet über sie, sobald sie weitergegeben ist, und ändert ihren Zustand nie. Die Reaktionen sind zugleich die Warteschlange, aus der sich die Instanzen ihre Arbeit holen. Gibt eine Reaktion auf, steht sie auf `Error`, bis ein Admin sie zurücksetzt; dann bekommt sie wieder alle Zustellversuche. Wird ihre Lieferung gelöscht, endet eine Reaktion, die noch nicht erledigt ist, als `Deleted`. Im Code: `DeliveryReaction` mit `ReactionKind.Transfer` oder `ReactionKind.Notification`, geholt über `ReactionStore`; welche Lieferungen noch nicht weitergegeben sind, sagt `UnforwardedDeliveryIds`.

## Hold

Die Zeit, in der eine Reaktion noch nicht fällig ist, weil der Lieferant die Lieferung noch löschen darf: bis zum Ende des Löschfensters. Im Code: der erste Wert von `DeliveryReaction.NextAttemptAt`.

## Zustellversuch

Ein Durchgang einer Reaktion durch ihren Kanal. Er zählt, sobald eine Instanz die Reaktion holt, und hält sie bis zum Zeitlimit plus eine Minute; meldet er sich bis dahin nicht zurück, übernimmt eine andere Instanz. Nach einem misslungenen Zustellversuch startet der nächste um den Abstand aus `Delivery:Reactions:RetryDelays` nach dem Start des misslungenen, ist dieser Zeitpunkt schon vorbei, sofort; nach dem letzten steht die Reaktion auf `Error`. Läuft die App durch, steht damit fest, wann eine Reaktion spätestens endet. Ein Ausfall der App verschiebt das Ende um seine Dauer, ohne dass ein Zustellversuch verloren geht. Unterbricht ein Stopp der Anwendung einen Zustellversuch, trägt sie ihn als misslungen ein, und er zählt mit. Im Code: `DeliveryReaction.Attempts`, `ReactionStore.ClaimNextAsync`, ausgeführt von `ReactionWorker`.

## Satz

Eine benannte Zusammenstellung von Reaktionen in der Pipeline-Definition, unter `reaction_sets`: Lieferungs-Schritte unter `transfers`, Benachrichtigungen unter `notifications`, mindestens eine Reaktion. Ein Mandat wählt einen Satz und füllt die Werte, die der Satz unter `mandate_config` von ihm verlangt, pro Reaktion. Im Code: `ReactionSetConfig`, am Mandat `ReactionSetId` und `ReactionSetValues`.

## Kanal

Was eine Reaktion ausführt, etwa die Ablage. Die Pipeline-Definition führt die Kanäle unter `channels`, und eine Reaktion verweist mit `channel_id` auf einen davon, so wie ein Schritt auf einen Prozess. Jeder Wert eines Kanals kommt von genau einer Stelle: aus der Basis-Konfiguration (`Pipeline:ChannelConfigs`), aus der Pipeline-Definition (`default_config` des Kanals, in der Reaktion überschreibbar) oder, nur für Parameter mit `[MandateValue]`, vom Mandat. Im Code: `ChannelConfig` in der Definition, `IDeliveryChannel`, erzeugt von `DeliveryChannelFactory`.

## Info-Mail

Eine Benachrichtigung über den Mail-Kanal an die Empfänger, die das Mandat oder der Satz nennt. Mit dem Schalter `includeDeclarer` geht sie auch an den Lieferanten und ist damit die Quittung aus ADR 0015; eine eigene Quittung neben den Sätzen gibt es nicht. Ihren Text bringt ein Template aus dem Ressourcen-Verzeichnis. Im Code: `MailChannel`, das Template in `MailTemplate`.
