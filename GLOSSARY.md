# Begriffe

Die Begriffe rund um den Lebenszyklus eines Verarbeitungsjobs und seiner Lieferung, so wie sie in Issues, Specs, Reviews und im Code verwendet werden. Heisst ein Begriff im Code anders, steht der Name dort dabei. Wörter, die wir für einen Begriff nicht verwenden, stehen bei ihm unter *Vermeiden*. Zeigt die Oberfläche einen Begriff, stehen bei ihm unter *Übersetzungen* die Wörter, die sie auf Englisch, Französisch und Italienisch dafür verwendet, mit dem unbestimmten Artikel für das Geschlecht; neue Texte in `src/Geopilot.Frontend/public/locale` folgen ihnen. Ein Begriff ohne Übersetzungen erscheint nicht in der Oberfläche, sein englischer Name ist der im Code. Ändert sich ein Begriff oder kommt einer dazu, wird diese Datei im selben PR nachgeführt.

## Job

Alles, was geopilot zur Verarbeitung eines Uploads mit einem Mandat im Speicher hält: Zustand, Pipeline und den Verweis auf den Upload. Ein Job entsteht beim Start und lebt bis zu seinem Retirement. Pro Upload ist höchstens ein Job aktiv, also Pending oder Running; ein zweiter Start wird abgelehnt, bis dieser Job endet. Scheitert der Start, bleibt kein Job zurück. Im Code: `ProcessingJob`, gestartet in `JobLifecycle.StartAsync`.

_Übersetzungen_: EN job, FR un job, IT un job

## Protokoll

Was das Ausführungsprotokoll zu einem Job festhält, den ganzen Weg vom Start über Preflight und die Schritte der Pipeline bis zur Lieferung und ihren Reaktionen, auch wenn diese Tage nach dem Ende der Pipeline kommen. Heute gehört zu jedem Job genau ein Protokoll. Anders als der Job überlebt es das Retirement und Neustarts, siehe [Ausführungsprotokoll](docs/Ausfuehrungsprotokoll.md). Sein Verarbeitungszustand ist der Zustand seiner Pipeline, nicht der seiner Lieferung. Im Code: `Recording`, geschrieben vom `Recorder`.

_Übersetzungen_: EN recording, FR un enregistrement, IT una registrazione

## Ausgang

Wie die Pipeline eines Jobs geendet hat, gemeldet vom Runner: zu Ende gelaufen mit dem Zustand, den die Pipeline meldet, Zeitüberschreitung, Herunterfahren oder Absturz. Lässt eine Bedingung die Daten scheitern, läuft die Pipeline mit `Failed` zu Ende. Ein Absturz ist dagegen eine Exception, die den Lauf abbricht: Ein Prozessor wirft, oder das Speichern der Downloads oder das Stagen der Lieferdateien scheitert. Im Code: `RunOutcome`.

## Folgen

Was aus einem Ausgang für den Job folgt, soweit es vom Ausgang abhängt: sein neuer Zustand und ob der Upload freigegeben wird. Was bei jedem Ausgang geschieht, etwa die Pipeline entsorgen oder das Protokoll schreiben, gehört nicht dazu. Im Code: `JobDecision`, entschieden in `JobLifecycle.Decide`.

## Freigabe

Das Löschen der hochgeladenen Dateien eines Jobs aus dem Upload-Speicher. Ob sie am Laufende geschieht, entscheidet `JobLifecycle.Decide`. Ein Upload, der nicht freigegeben wird, bleibt für einen erneuten Start liegen, bis seine Session abläuft (`Upload:CleanupAgeHours`). Im Code: `IUploadOrchestrationService.ReleaseUploadAsync`.

## Retirement

Das Entfernen eines Jobs, wenn seit dem Ende seines Laufs `Processing:JobRetention` vergangen ist: der Eintrag im Speicher sowie seine Downloads, Visualisierungen, Arbeits- und Staging-Verzeichnisse. Lieferungen und sein Protokoll bleiben erhalten. Den Upload gibt das Retirement nicht frei, der ist dann schon freigegeben oder wird bis zum Ablauf seiner Session behalten. Im Code: `ProcessingJobCleanupService`, für den Job selbst `JobLifecycle.Retire`.

## Lieferberechtigt

Ob aus einem Job je eine Lieferung werden kann: Wer ihn startet, ist Mitglied einer Organisation des Mandats, und das Mandat nimmt Lieferungen an. Dieselbe Regel entscheidet `allowDelivery` in der Mandatsübersicht; ein öffentliches Mandat öffnet sie Nichtmitgliedern nicht. Entschieden wird einmal beim Start. Einen Job, den niemand abgeben darf, staged der Runner nicht, und seine Lieferung wird abgelehnt, wer auch immer sie verlangt. Im Code: `ProcessingJob.MayDeliver`, entschieden in `MandateService.MayDeliverAsync`.

_Übersetzungen_: EN permitted to deliver, FR autorisé à livrer, IT autorizzato a consegnare

## Staging

Wo die Lieferdateien eines Jobs warten, bis jemand die Lieferung abgibt. Endet der Lauf lieferbar und ist der Job lieferberechtigt, kopiert der Runner die Dateien, die Schritte mit der Ausgabeaktion `Delivery` markieren, nach `<Storage:AssetsDirectory>/staging/<jobId>`. Die Abgabe benennt dieses Verzeichnis in das Lieferverzeichnis um, ohne Abgabe geht es mit dem Retirement. Eine Staging-Umgebung ist nie gemeint. Im Code: `IAssetStagingFileStore`, gefüllt von `ProcessingRunner.ExtractDeliveryFilesAsync`, umbenannt von `IAssetFileStore.PromoteStagedFiles`.

## Lieferung

Was ein Lieferant nach einem lieferbaren Lauf übergibt: die Dateien aus dem Staging und die Lieferangaben. Im Web gibt der Lieferant sie selbst ab, bei einem Versuch erstellt geopilot sie aus der Lieferabsicht. Abgeschlossen ist sie, wenn ihr Löschfenster verstrichen ist und alle ihre Weitergaben erfolgreich waren; Benachrichtigungen ändern daran nichts. Lieferant ist eine Person oder ein Maschinen-Client. Die Dateien einer Lieferung liegen in ihrem Lieferverzeichnis `<Storage:AssetsDirectory>/<jobId>`. Im Code: `Delivery`, erstellt von `IDeliveryCreationService.CreateAsync`, der Lieferant als `Supplier`; bis zur Umbenennung hiess die Abgabe dort `DeclareAsync`.

_Übersetzungen_: EN delivery, FR une livraison, IT una consegna

_Vermeiden_: Deklaration, Anlieferung, Auslieferung

## Versuch

Eine maschinelle Lieferung aus Sicht des Aufrufers: Mandat, Lieferangaben und Daten, die er übergibt, bis die Lieferung erstellt oder abgelehnt ist. Ein Versuch lebt im Speicher und höchstens so lange wie sein Job, siehe [Maschinelle Lieferung](docs/MaschinelleLieferung.md). Im Code: ein `ProcessingJob` mit `DeliveryIntent`, abgefragt über den `SubmissionController`.

_Übersetzungen_: EN attempt, FR une tentative, IT un tentativo

## Verantwortliche Person

Eine Person, die ein Admin einem Maschinen-Client zuordnet, auf der Seite des Clients; die Seite der Person zeigt sie als «Verantwortlich für Maschinen-Clients». Sie sieht die Lieferungen des Clients unter «Meine Lieferungen», auch die vor der Zuordnung und die eines deaktivierten Clients, darf sie im Löschfenster löschen wie eigene und bekommt die Info-Mail mit `includeSupplier`, solange sie aktiv ist. Rechte auf die Mandate des Clients bekommt sie dadurch nicht, und gemeinsame Organisationen machen niemanden verantwortlich. Im Code: `MachineClient.ResponsibleUsers`, Tabelle `MachineClientUser`.

_Übersetzungen_: EN responsible user, FR une personne responsable, IT una persona responsabile

## Lieferabsicht

Der beim Start geäusserte Wunsch, dass geopilot die Lieferung selbst erstellt, sobald der Lauf lieferbar endet. Heute hat nur ein Versuch eine Lieferabsicht. Sie wird beim Anlegen des Jobs an ihn gebunden, mit Lieferant und Lieferangaben, sodass kein Lauf enden kann, bevor sie bekannt ist. Wird die Lieferung abgewiesen, steht der Grund auf dem Job; gelingt sie, ist die Lieferung selbst der Beleg. Im Code: `ProcessingJob.DeliveryIntent`, erstellt in `JobLifecycle.RunFinishedAsync`.

## Löschfenster

Die Zeit nach der Abgabe, in der ein Lieferant seine Lieferung noch selbst löschen darf (`Delivery:UploaderDeleteEnabled`), bei einem Maschinen-Client eine verantwortliche Person. Es schliesst nach `Delivery:DeleteDuration` oder beim nächsten Zeitpunkt aus `Delivery:DeleteRestrictInterval`, je nachdem, was früher kommt; das Intervall gilt in der Zeitzone des Servers. Ohne beide Grenzen schliesst es nie. Admins löschen unabhängig davon. Im Code: `DeliveryOptions.GetDeleteWindowEnd`.

## Reaktion

Der Einsatz eines Kanals für eine Lieferung, als Weitergabe oder als Benachrichtigung; die Reaktionen einer Lieferung sind alle Kanäle, die ihr Satz einsetzt. Keine beginnt, bevor die Lieferung nicht mehr zurückgezogen werden kann (ADR 0015). Die Reaktionen sind zugleich die Warteschlange, aus der sich die Instanzen ihre Arbeit holen. Gibt eine Reaktion auf, steht sie auf `Error`, bis ein Admin sie zurücksetzt; dann bekommt sie wieder alle Zustellversuche. Wird ihre Lieferung gelöscht, endet eine Reaktion, die noch nicht erledigt ist, als `Deleted`. Im Code: `DeliveryReaction`, geholt über `ReactionStore`.

_Übersetzungen_: EN reaction, FR une réaction, IT una reazione

## Weitergabe

Eine Reaktion, deren Kanal ein Satz unter `transfers` einsetzt. Sie gehört zur Lieferung: Erst wenn alle Weitergaben einer Lieferung erfolgreich waren, ist sie weitergegeben, und erst dann zeigt sie der STAC-Katalog. Weil Weitergaben erst nach dem Löschfenster beginnen, ist sie damit auch abgeschlossen. Eine Lieferung ohne Weitergaben gilt dagegen schon mit der Abgabe als weitergegeben und erscheint dann im STAC-Katalog, abgeschlossen ist sie erst nach dem Löschfenster. Steht eine Weitergabe auf `Error`, gilt die Lieferung nicht als weitergegeben. Im Code: `ReactionKind.Transfer`; welche Lieferungen noch nicht weitergegeben sind, sagt `UnforwardedDeliveryIds`.

_Übersetzungen_: EN forwarding, FR une transmission, IT un inoltro

_Vermeiden_: Lieferungs-Schritt

## Benachrichtigung

Eine Reaktion, deren Kanal ein Satz unter `notifications` einsetzt, etwa eine Info-Mail. Sie berichtet über eine Lieferung, sobald diese abgeschlossen ist, und ändert ihren Zustand nie: Scheitert eine Benachrichtigung, bleibt die Lieferung abgeschlossen. Im Code: `ReactionKind.Notification`.

_Übersetzungen_: EN notification, FR une notification, IT una notifica

## Hold

Die Zeit, in der eine Reaktion noch nicht fällig ist, weil der Lieferant die Lieferung noch löschen darf: bis zum Ende des Löschfensters. Im Code: der erste Wert von `DeliveryReaction.NextAttemptAt`.

## Zustellversuch

Ein Durchgang einer Reaktion durch ihren Kanal. Er zählt, sobald eine Instanz die Reaktion holt, und hält sie bis zum Zeitlimit plus eine Minute; meldet er sich bis dahin nicht zurück, übernimmt eine andere Instanz. Nach einem misslungenen Zustellversuch startet der nächste um den Abstand aus `Delivery:Reactions:RetryDelays` nach dem Start des misslungenen, ist dieser Zeitpunkt schon vorbei, sofort; nach dem letzten steht die Reaktion auf `Error`. Läuft die App durch, steht damit fest, wann eine Reaktion spätestens endet. Ein Ausfall der App verschiebt das Ende um seine Dauer, ohne dass ein Zustellversuch verloren geht. Unterbricht ein Stopp der Anwendung einen Zustellversuch, trägt sie ihn als misslungen ein, und er zählt mit. In der Oberfläche heisst er kurz Versuch. Im Code: `DeliveryReaction.Attempts`, `ReactionStore.ClaimNextAsync`, ausgeführt von `ReactionWorker`.

_Übersetzungen_: EN attempt, FR une tentative, IT un tentativo

## Satz

Eine benannte Zusammenstellung von Reaktionen in der Pipeline-Definition, unter `reaction_sets`: Weitergaben unter `transfers`, Benachrichtigungen unter `notifications`, mindestens eine Reaktion. Ein Mandat wählt einen Satz und füllt die Werte, die der Satz unter `mandate_config` von ihm verlangt, pro Reaktion. Im Code: `ReactionSetConfig`, am Mandat `ReactionSetId` und `ReactionSetValues`.

_Übersetzungen_: EN reaction set, FR un ensemble de réactions, IT un insieme di reazioni

## Kanal

Was eine Reaktion ausführt, etwa die Ablage. Ob ein Kanal als Weitergabe oder als Benachrichtigung dient, legt nicht er fest, sondern der Satz, der ihn einsetzt. Die Pipeline-Definition führt die Kanäle unter `channels`, und eine Reaktion verweist mit `channel_id` auf einen davon, so wie ein Schritt auf einen Prozess. Jeder Wert eines Kanals kommt von genau einer Stelle: aus der Basis-Konfiguration (`Pipeline:ChannelConfigs`), aus der Pipeline-Definition (`default_config` des Kanals, in der Reaktion überschreibbar) oder, nur für Parameter mit `[MandateValue]`, vom Mandat. Im Code: `ChannelConfig` in der Definition, `IDeliveryChannel`, erzeugt von `DeliveryChannelFactory`.

## Info-Mail

Eine Benachrichtigung über den Mail-Kanal an die Empfänger, die das Mandat oder der Satz nennt. Mit dem Schalter `includeSupplier` geht sie auch an den Lieferanten, bei einem Maschinen-Client an seine verantwortlichen Personen, und ist damit die Quittung aus ADR 0015; eine eigene Quittung neben den Sätzen gibt es nicht. Ihren Text bringt ein Template aus dem Ressourcen-Verzeichnis. Im Code: `MailChannel`, das Template in `MailTemplate`.
