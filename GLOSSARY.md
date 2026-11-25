# Begriffe

Die Begriffe rund um den Lebenszyklus einer [Verarbeitung](#verarbeitung) und ihrer [Lieferung](#lieferung), so wie sie in Issues, Specs, Reviews und im Code verwendet werden. Heisst ein Begriff im Code anders, steht der Name dort dabei. Wörter, die wir für einen Begriff nicht verwenden, stehen bei ihm unter *Vermeiden*. Zeigt die Oberfläche einen Begriff, stehen bei ihm unter *Übersetzungen* die Wörter, die sie auf Englisch, Französisch und Italienisch dafür verwendet, mit dem unbestimmten Artikel für das Geschlecht; neue Texte in `src/Geopilot.Frontend/public/locale` folgen ihnen. Ein Begriff ohne Übersetzungen erscheint nicht in der Oberfläche, sein englischer Name ist der im Code. Ändert sich ein Begriff oder kommt einer dazu, wird diese Datei im selben PR nachgeführt.

## Pipeline

Eine benannte Abfolge von Schritten; jeder Schritt führt einen Prozess aus, etwa eine Validierung. Ein Mandat wählt eine Pipeline, und jede [Verarbeitung](#verarbeitung) für das Mandat führt sie aus: Dafür baut geopilot aus der Pipeline eine eigene Instanz, die nach dem Lauf entsorgt wird. Alle Pipelines einer Installation stehen in der Pipeline-Definition, einer YAML-Datei, unter `pipelines`, zusammen mit den Prozessen, [Kanälen](#kanal) und [Reaktions-Sätzen](#reaktions-satz) (`Pipeline:Definition`). Im Code: `PipelineConfig`, die Instanz `IPipeline`, am Mandat `PipelineId`.

_Übersetzungen_: EN pipeline, FR un pipeline, IT una pipeline

## Verarbeitung

Was geopilot mit einem Upload für ein Mandat tut, vom Start bis zu ihrem [Retirement](#retirement); die [Pipeline](#pipeline) des Mandats beschreibt sie. Ihr Lauf ist die Ausführung der Pipeline nach dem Preflight; endet er mit `Success` oder `Warning`, ist er lieferbar. Im Speicher hält eine Verarbeitung ihren Zustand, ihre Pipeline und den Verweis auf den Upload. Pro Upload ist höchstens eine Verarbeitung aktiv, also Pending oder Running; ein zweiter Start wird abgelehnt, bis diese endet. Scheitert der Start, bleibt keine Verarbeitung zurück. Im Code: `ProcessingJob`, gestartet in `JobLifecycle.StartAsync`.

_Übersetzungen_: EN processing, FR un traitement, IT un'elaborazione

_Vermeiden_: Job, Verarbeitungsjob, Prozessierung

## Protokoll

Was das Ausführungsprotokoll zu einer [Verarbeitung](#verarbeitung) festhält, den ganzen Weg vom Start über Preflight und die Schritte der [Pipeline](#pipeline) bis zur [Lieferung](#lieferung) und ihren [Reaktionen](#reaktion), auch wenn diese Tage nach dem Ende der Pipeline kommen. Heute gehört zu jeder Verarbeitung genau ein Protokoll. Anders als die Verarbeitung überlebt es das [Retirement](#retirement) und Neustarts, siehe [Ausführungsprotokoll](docs/Ausfuehrungsprotokoll.md). Sein Verarbeitungszustand ist der Zustand seiner Pipeline, nicht der seiner Lieferung. Im Code: `Recording`, geschrieben vom `Recorder`.

_Übersetzungen_: EN recording, FR un enregistrement, IT una registrazione

## Ausgang

Wie die [Pipeline](#pipeline) einer [Verarbeitung](#verarbeitung) geendet hat, gemeldet vom Runner: zu Ende gelaufen mit dem Zustand, den die Pipeline meldet, Zeitüberschreitung, Herunterfahren oder Absturz. Lässt eine Bedingung die Daten scheitern, läuft die Pipeline mit `Failed` zu Ende. Ein Absturz ist dagegen eine Exception, die den Lauf abbricht: Ein Prozessor wirft, oder das Speichern der Downloads oder das Stagen der Lieferdateien scheitert. Im Code: `RunOutcome`.

## Folgen

Was aus einem [Ausgang](#ausgang) für die [Verarbeitung](#verarbeitung) folgt, soweit es vom Ausgang abhängt: ihr neuer Zustand und ob der Upload freigegeben wird. Was bei jedem Ausgang geschieht, etwa die [Pipeline](#pipeline) entsorgen oder das [Protokoll](#protokoll) schreiben, gehört nicht dazu. Im Code: `JobDecision`, entschieden in `JobLifecycle.Decide`.

## Freigabe

Das Löschen der hochgeladenen Dateien einer [Verarbeitung](#verarbeitung) aus dem Upload-Speicher. Ob sie am Laufende geschieht, entscheidet `JobLifecycle.Decide`. Ein Upload, der nicht freigegeben wird, bleibt für einen erneuten Start liegen, bis seine Session abläuft (`Upload:CleanupAgeHours`). Im Code: `IUploadOrchestrationService.ReleaseUploadAsync`.

## Retirement

Das Entfernen einer [Verarbeitung](#verarbeitung), wenn seit dem Ende ihres Laufs `Processing:JobRetention` vergangen ist: der Eintrag im Speicher sowie ihre Downloads, Visualisierungen, Arbeits- und Staging-Verzeichnisse. [Lieferungen](#lieferung) und ihr [Protokoll](#protokoll) bleiben erhalten. Den Upload gibt das Retirement nicht frei, der ist dann schon freigegeben oder wird bis zum Ablauf seiner Session behalten. Im Code: `ProcessingJobCleanupService`, für die Verarbeitung selbst `JobLifecycle.Retire`.

## Lieferberechtigt

Ob aus einer [Verarbeitung](#verarbeitung) je eine [Lieferung](#lieferung) werden kann: Wer sie startet, ist Mitglied einer Organisation des Mandats, und das Mandat nimmt Lieferungen an. Dieselbe Regel entscheidet `allowDelivery` in der Mandatsübersicht; ein öffentliches Mandat öffnet sie Nichtmitgliedern nicht. Entschieden wird einmal beim Start. Ist eine Verarbeitung nicht lieferberechtigt, staged der Runner nichts, und ihre Lieferung wird abgewiesen, wer auch immer sie verlangt. Nicht zu verwechseln mit [lieferbar](#verarbeitung): Das sagt, ob die Daten eine Lieferung zulassen. Im Code: `ProcessingJob.MayDeliver`, entschieden in `MandateService.MayDeliverAsync`.

_Übersetzungen_: EN permitted to deliver, FR autorisé à livrer, IT autorizzato a consegnare

## Staging

Wo die Lieferdateien einer [Verarbeitung](#verarbeitung) bis zur Abgabe der [Lieferung](#lieferung) warten. Endet der Lauf lieferbar und ist die Verarbeitung [lieferberechtigt](#lieferberechtigt), kopiert der Runner die Dateien, die Schritte mit der Ausgabeaktion `Delivery` markieren, nach `<Storage:AssetsDirectory>/staging/<jobId>`. Die Abgabe benennt dieses Verzeichnis in das Lieferverzeichnis um, sobald die Lieferung gespeichert ist; ohne Abgabe geht es mit dem [Retirement](#retirement). Eine Staging-Umgebung ist nie gemeint. Im Code: `IAssetStagingFileStore`, gefüllt von `ProcessingRunner.ExtractDeliveryFilesAsync`, umbenannt von `IAssetFileStore.PromoteStagedFiles`.

## Lieferung

Was ein Lieferant nach einem lieferbaren Lauf übergibt: die Dateien aus dem [Staging](#staging) und die Lieferangaben. Mit der Abgabe entsteht sie: Im Web gibt der Lieferant sie selbst ab, bei einem [Versuch](#versuch) erstellt geopilot sie aus der [Lieferabsicht](#lieferabsicht). Abgeschlossen ist sie, sobald der Lieferant sie nicht mehr löschen darf, also nach dem [Löschfenster](#löschfenster); weitergegeben ist sie, wenn danach alle ihre [Weitergaben](#weitergabe) erfolgreich waren. Lieferant ist eine Person oder ein Maschinen-Client. Die Dateien einer Lieferung liegen in ihrem Lieferverzeichnis `<Storage:AssetsDirectory>/<jobId>`. Im Code: `Delivery`, erstellt von `IDeliveryCreationService.CreateAsync`, der Lieferant als `Supplier`; bis zur Umbenennung hiess die Abgabe dort `DeclareAsync`.

_Übersetzungen_: EN delivery, FR une livraison, IT una consegna

_Vermeiden_: Deklaration, Anlieferung, Auslieferung

## Versuch

Ein Anlauf eines Maschinen-Clients zu einer [Lieferung](#lieferung): Er übergibt Mandat, Lieferangaben und Daten und fragt ab, bis die Lieferung erstellt ist oder feststeht, dass keine entsteht. Lehnt die [Pipeline](#pipeline) die Daten ab, endet er als `rejected`; scheitert der Lauf oder weist geopilot die Lieferung ab, als `failed`. Ein Versuch steht im [Protokoll](#protokoll) seiner [Verarbeitung](#verarbeitung) und ist höchstens so lange abfragbar wie diese, siehe [Maschinelle Lieferung](docs/MaschinelleLieferung.md). Im Code: ein `ProcessingJob` mit `DeliveryIntent`, im Protokoll ein `Recording` mit `SubmissionMandateKey`, abgefragt über den `SubmissionController`.

_Übersetzungen_: EN attempt, FR une tentative, IT un tentativo

## Verantwortliche Person

Eine Person, die ein Admin einem Maschinen-Client zuordnet, auf der Seite des Clients; die Seite der Person zeigt sie als «Verantwortlich für Maschinen-Clients». Sie sieht die [Lieferungen](#lieferung) des Clients unter «Meine Lieferungen», auch die vor der Zuordnung und die eines deaktivierten Clients, darf sie im [Löschfenster](#löschfenster) löschen wie eigene und bekommt die [Info-Mail](#info-mail) mit `includeSupplier`, solange sie aktiv ist. Rechte auf die Mandate des Clients bekommt sie dadurch nicht, und gemeinsame Organisationen machen niemanden verantwortlich. Im Code: `MachineClient.ResponsibleUsers`, Tabelle `MachineClientUser`.

_Übersetzungen_: EN responsible user, FR une personne responsable, IT una persona responsabile

## Lieferabsicht

Der Wunsch, mit dem ein [Versuch](#versuch) startet: dass geopilot die [Lieferung](#lieferung) selbst erstellt, sobald der Lauf lieferbar endet. Nur eine maschinelle Lieferung hat eine Lieferabsicht; im Web gibt der Lieferant selbst ab. Sie wird beim Start der [Verarbeitung](#verarbeitung) an sie gebunden, mit Lieferant und Lieferangaben, sodass kein Lauf enden kann, bevor sie bekannt ist. Wird die Lieferung abgewiesen, steht der Grund auf der Verarbeitung; gelingt sie, ist die Lieferung selbst der Beleg. Im Code: `ProcessingJob.DeliveryIntent`, gebunden in `JobLifecycle.StartAsync` und eingelöst in `JobLifecycle.RunFinishedAsync`.

## Löschfenster

Die Zeit nach der Abgabe, in der ein Lieferant seine [Lieferung](#lieferung) noch selbst löschen darf (`Delivery:UploaderDeleteEnabled`), bei einem Maschinen-Client eine [verantwortliche Person](#verantwortliche-person). Dürfen Lieferanten nicht löschen, gibt es keines. Es schliesst nach `Delivery:DeleteDuration` oder beim nächsten Zeitpunkt aus `Delivery:DeleteRestrictInterval`, je nachdem, was früher kommt; das Intervall gilt in der Zeitzone des Servers. Ohne beide Grenzen schliesst es nie. Admins löschen unabhängig davon. Im Code: `DeliveryOptions.GetDeleteWindowEnd`.

## Reaktion

Der Einsatz eines [Kanals](#kanal) für eine [Lieferung](#lieferung), als [Weitergabe](#weitergabe) oder als [Benachrichtigung](#benachrichtigung); die Reaktionen einer Lieferung sind alle Einsätze, die der [Reaktions-Satz](#reaktions-satz) ihres Mandats vorsieht. Keine beginnt, bevor die Lieferung abgeschlossen ist (ADR 0015). Die Reaktionen sind zugleich die Warteschlange, aus der sich die Instanzen ihre Arbeit holen. Gibt eine Reaktion auf, steht sie auf `Error`, bis ein Admin sie zurücksetzt; dann bekommt sie wieder alle [Zustellversuche](#zustellversuch). Wird ihre Lieferung gelöscht, endet eine Reaktion, die noch nicht erledigt ist, als `Deleted`. Im Code: `DeliveryReaction`, geholt über `ReactionStore`.

_Übersetzungen_: EN reaction, FR une réaction, IT una reazione

## Weitergabe

Eine [Reaktion](#reaktion), deren [Kanal](#kanal) ein [Reaktions-Satz](#reaktions-satz) unter `transfers` einsetzt. Sie beginnt, wenn die [Lieferung](#lieferung) abgeschlossen ist, und gehört zu ihr: Erst wenn alle Weitergaben einer Lieferung erfolgreich waren, ist sie weitergegeben, und erst dann zeigt sie der STAC-Katalog. Eine Lieferung ohne Weitergaben ist weitergegeben, sobald sie abgeschlossen ist. Steht eine Weitergabe auf `Error`, gilt die Lieferung nicht als weitergegeben. Im Code: `ReactionKind.Transfer`; welche Lieferungen eine offene Weitergabe haben, sagt `UnforwardedDeliveryIds`.

_Übersetzungen_: EN forwarding, FR une transmission, IT un inoltro

_Vermeiden_: Lieferungs-Schritt

## Benachrichtigung

Eine [Reaktion](#reaktion), deren [Kanal](#kanal) ein [Reaktions-Satz](#reaktions-satz) unter `notifications` einsetzt, etwa eine [Info-Mail](#info-mail). Sie berichtet über eine [Lieferung](#lieferung), sobald diese weitergegeben ist, und ändert ihren Zustand nie: Scheitert eine Benachrichtigung, bleibt die Lieferung weitergegeben. Im Code: `ReactionKind.Notification`.

_Übersetzungen_: EN notification, FR une notification, IT una notifica

## Hold

Die Zeit, in der eine [Reaktion](#reaktion) noch nicht fällig ist, weil der Lieferant die [Lieferung](#lieferung) noch löschen darf: bis zum Ende des [Löschfensters](#löschfenster). Im Code: der erste Wert von `DeliveryReaction.NextAttemptAt`.

## Zustellversuch

Ein Durchgang einer [Reaktion](#reaktion) durch ihren [Kanal](#kanal). Er zählt, sobald eine Instanz die Reaktion holt, und hält sie bis zum Zeitlimit plus eine Minute; meldet er sich bis dahin nicht zurück, übernimmt eine andere Instanz. Nach einem misslungenen Zustellversuch startet der nächste um den Abstand aus `Delivery:Reactions:RetryDelays` nach dem Start des misslungenen, ist dieser Zeitpunkt schon vorbei, sofort; nach dem letzten steht die Reaktion auf `Error`. Läuft geopilot durch, steht damit fest, wann eine Reaktion spätestens endet. Ein Ausfall von geopilot verschiebt das Ende um seine Dauer, ohne dass ein Zustellversuch verloren geht. Unterbricht ein Stopp von geopilot einen Zustellversuch, trägt geopilot ihn als misslungen ein, und er zählt mit. In der Oberfläche heisst er kurz Versuch. Im Code: `DeliveryReaction.Attempts`, `ReactionStore.ClaimNextAsync`, ausgeführt von `ReactionWorker`.

_Übersetzungen_: EN attempt, FR une tentative, IT un tentativo

## Reaktions-Satz

Eine benannte Zusammenstellung von [Reaktionen](#reaktion) in der [Pipeline-Definition](#pipeline), unter `reaction_sets`: [Weitergaben](#weitergabe) unter `transfers`, [Benachrichtigungen](#benachrichtigung) unter `notifications`, mindestens eine Reaktion. Ein Mandat wählt einen Reaktions-Satz und füllt die Werte, die der Reaktions-Satz unter `mandate_config` von ihm verlangt, pro Reaktion. Im Code: `ReactionSetConfig`, am Mandat `ReactionSetId` und `ReactionSetValues`.

_Übersetzungen_: EN reaction set, FR un ensemble de réactions, IT un insieme di reazioni

_Vermeiden_: Satz, Satz von Reaktionen

## Kanal

Was eine [Reaktion](#reaktion) ausführt, etwa die Ablage. Ob ein Kanal als [Weitergabe](#weitergabe) oder als [Benachrichtigung](#benachrichtigung) dient, legt nicht er fest, sondern der [Reaktions-Satz](#reaktions-satz), der ihn einsetzt. Die [Pipeline-Definition](#pipeline) führt die Kanäle unter `channels`, und eine Reaktion verweist mit `channel_id` auf einen davon, so wie ein Schritt auf einen Prozess. Jeder Wert eines Kanals kommt von genau einer Stelle: aus der Basis-Konfiguration (`Pipeline:ChannelConfigs`), aus der Pipeline-Definition (`default_config` des Kanals, in der Reaktion überschreibbar) oder, nur für Parameter mit `[MandateValue]`, vom Mandat. Im Code: `ChannelConfig` in der Definition, `IDeliveryChannel`, erzeugt von `DeliveryChannelFactory`.

## Info-Mail

Eine [Benachrichtigung](#benachrichtigung) über den Mail-Kanal an die Empfänger, die das Mandat oder der [Reaktions-Satz](#reaktions-satz) nennt. Mit dem Schalter `includeSupplier` geht sie auch an den Lieferanten, bei einem Maschinen-Client an seine [verantwortlichen Personen](#verantwortliche-person), und ist damit die Quittung aus ADR 0015; eine eigene Quittung neben den Reaktions-Sätzen gibt es nicht. Ihren Text bringt ein Template aus dem Ressourcen-Verzeichnis. Im Code: `MailChannel`, das Template in `MailTemplate`.
