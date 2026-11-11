# Ausführungsprotokoll

geopilot hält zu jedem Verarbeitungsjob ein dauerhaftes Ausführungsprotokoll in der Datenbank fest: was lief, auf welcher Fassung der Pipeline-Definition, für wen, und wie es endete. Das Protokoll ist der Prüfnachweis des Lieferprozesses. Es überlebt Neustarts und die Job-Retention, und zwar ausdrücklich auch für abgelehnte und abgebrochene Läufe, die sonst keine Spur hinterlassen.

Das Protokoll wird nur geschrieben. Kein Ausführungspfad liest es, um Entscheidungen zu treffen: es beschreibt die Vergangenheit, es macht sie nicht wieder ausführbar.

## Was aufgezeichnet wird

Pro Job entsteht beim Start ein Datensatz (`PipelineRuns`) mit:

- der gelaufenen Pipeline und einem **Definitions-Snapshot** (siehe unten),
- Mandat und Urheber, entweder der Benutzer oder der Maschinen-Client (`UserId` oder `MachineClientId`, nie beide zugleich, was die Datenbank erzwingt; beide leer, wenn der Job anonym auf einem öffentlichen Mandat gestartet wurde; anonym darf prozessiert, aber nicht geliefert werden, und wer die Lieferung deklariert hat, steht an der Lieferung selbst) sowie der Art des Clients (`WebClient`, `ApiClient`, `Unknown`), klassifiziert aus dem Request und nie als roher Header gespeichert. Die Art des Clients sagt, auf welchem Weg der Job gestartet wurde, nicht von wem: eine Person, die die API direkt aufruft, etwa aus Swagger, ist ebenso `ApiClient` wie ein Maschinen-Client, und wer es war, steht in den beiden Id-Spalten,
- bei einer maschinellen Lieferung der Kennung, die der Client für die Lieferung mitgeschickt hat (`ClientReference`), so wie sie kam, ohne Prüfung gegen frühere Versuche. Versuche mit derselben Kennung, demselben Urheber und demselben Mandat sind Versuche derselben Lieferung,
- der **Deklarierbarkeit** (`Declarability`), also ob der Urheber auf dem Mandat abgeben darf, unabhängig davon, ob die Daten eine Lieferung zulassen: `Declarable`, wenn der Urheber Mitglied einer Organisation des Mandats ist und das Mandat Lieferungen annimmt, sonst `NotDeclarable`, auch bei einem anonymen Start. Entschieden wird einmal beim Start. Läufe, die aufgezeichnet wurden, bevor das Protokoll diese Angabe kannte, haben keine (`NULL`): unbekannt, nicht «nicht deklarierbar»,
- dem Upload-Manifest (`PipelineRunFiles`): Dateiname, Storage-Key, deklarierte Grösse, und nach dem Virenscan der SHA-256 jeder Datei,
- dem Resultat des Virenscans (`Clean`, `ThreatDetected` mit Details, oder `NotScanned` wenn die Prüfung deaktiviert ist),
- der Applikationsversion und allen Zeitstempeln in UTC.

Pro Schritt (`PipelineRunSteps`) werden festgehalten: Status, Beginn und Ende, die Prozessor-Implementierung samt Assembly und Version, Fehlermeldung bei Abbruch, Statusmeldung, sowie die **Auswertung jeder geprüften Condition** (`PipelineRunConditions`): Ausdruck, optionale stabile Kennung (`id` in der Definition), die referenzierten Werte und ob sie zutraf. Auch nicht zutreffende Auswertungen werden festgehalten, damit "geprüft und nicht zugetroffen" von "nie geprüft" unterscheidbar bleibt. Die Namen der erzeugten Artefakte (Downloads, Visualisierungen, Lieferdateien) stehen in `PipelineRunArtifacts`.

Wirft ein Prozessor eine Exception, steht sie als Fehlermeldung an seinem Schritt. Am Lauf steht ein Grund (`FailureReason`) nur, wo kein Schritt ihn nennt: wenn der Preflight scheiterte, der Job in den Timeout lief, die Instanz herunterfuhr, oder das Ablegen der Downloads oder das Bereitstellen der Lieferdateien scheiterte.

Als Version eines Schritts (`ProcessAssemblyVersion`) hält das Protokoll, wie bei der Applikationsversion, die `InformationalVersion` der Assembly fest, aus der die Prozessor-Implementierung stammt; fehlt sie, die Assembly-Version. Bei einem Plugin nennt sie so den Build, der lief, samt Vorabzusatz: `1.4.0-preview.12`, wo die Assembly-Version nur `1.4.0.0` hergibt. Hält der Build seinen Commit fest, folgt dieser nach einem `+`. Die eingebauten Prozessoren stammen aus `Geopilot.Pipeline`, das eine feste Version hat und mit geopilot gebaut wird. Ihre Version nennt denselben Commit wie die Applikationsversion und sagt nichts, was diese nicht schon sagt.

Die **Eingaben** jedes Schritts stehen in `PipelineRunInputs`: was sein Prozessor beim Start bekam, eine Zeile pro Wert oder Datei, in der Reihenfolge seiner Parameter und auch innerhalb von Listen pro Element. Jede Zeile nennt den Parameter (`Parameter`), die Herkunft (`Origin`) und die Art (`Kind`: `Value`, `File` oder `Absent`):

- `Definition`: ein Wert, der in der Pipeline-Definition steht.
- `StepOutput`: ein Ergebnis eines früheren Schritts, in `Reference` als `schritt.Eigenschaft`. Ein Bezug auf einen übersprungenen Schritt steht als `Absent` da: Er trägt nichts bei, so wie bei der Bindung der Eingaben.
- `Resource`: eine Datei aus `${file(...)}`, in `Reference` ihr Pfad unter dem Ressourcenverzeichnis.
- `Upload`: eine Datei aus `${upload()}`.
- `Configuration`: eine Datei, die die Konfiguration des Prozessors über einen Parameter vom Typ `IPipelineFile` übergibt, etwa `refMappingFile`, in `Reference` ihr Pfad unter dem Ressourcenverzeichnis.

Ein Wert steht als Text in `Value` (Invariant Culture, nach 500 Zeichen abgeschnitten), eine Datei mit Name und Grösse (`FileName`, `Size`). Ressourcen und Dateien aus der Konfiguration tragen zusätzlich den SHA-256, berechnet beim Beginn des Schritts; so bleibt nachvollziehbar, welchen Stand einer Ressource ein Schritt bekam. Ein Upload trägt Grösse und SHA-256 aus dem Manifest, den SHA-256 nur, soweit der Virenscan ihn berechnet hat. Reicht ein früherer Schritt einen Upload unverändert weiter, etwa ein Matcher, markiert ihn `FromUpload`; seine Grösse kommt dann ebenfalls aus dem Manifest, einen Hash trägt er dort nicht, den trägt die Upload-Zeile des Schritts, der ihn aus dem Upload bekam. Andere Dateien aus früheren Schritten stehen ohne Hash da. Ein Schritt, dessen Prozessor nicht lief (übersprungen, per Pre-Condition gescheitert oder mit Eingaben, die sich nicht binden liessen), hat keine Eingaben; den Grund nennen seine Conditions oder seine Fehlermeldung.

Das Protokoll setzt dabei an der Grenze des Schritts an. Was ein Prozessor intern tut oder ein entfernter Dienst auflöst, etwa Modelle und Referenzdaten über `modelDirs`, hält es nicht fest; dafür bleibt das Log des Werkzeugs.

Scheitert bei einer [maschinellen Lieferung](MaschinelleAnlieferung.md) die Deklaration, die geopilot selbst vornimmt, obwohl die Daten eine Lieferung zuliessen, steht der Grund in `DeclarationFailure`: derselbe Text, den der Versuch in `messages` meldet. Er besteht aus festen Meldungen, nie aus dem Inhalt der Lieferangaben, denn ein Kommentar ist Freitext und gehört nicht in einen Datensatz, der jahrelang aufbewahrt wird. Leer heisst: geopilot hat deklariert oder hatte nichts zu deklarieren; bei einem älteren Lauf ohne `Declarability` ist es unbekannt. Ob eine Lieferung entstand, sagt die Lieferung selbst (`Deliveries.JobId`).

Wird die Lieferung eines Laufs weitergegeben (siehe [Reaktionen nach dem Abschluss](pipeline/Pipelines.md#reaktionen-nach-dem-abschluss)), steht jeder Zustellversuch ihrer Reaktionen in `PipelineRunReactionEvents`: die Lieferung, die Id der Reaktion im Satz, ob die Reaktion die Lieferung weitergibt oder begleitet, der Kanal, die Nummer des Versuchs, Beginn und Ende, der Zustand der Reaktion danach und die Meldung des Kanals. Die Meldung steht in `Message` pro Sprache (`de`, `fr`, `it`, `en`) als JSON-Objekt; was ein Server oder eine Ausnahme dazu meldet, steht in allen Sprachen unübersetzt darin. Personendaten des Lieferanten und Werte des Mandats stehen nicht darin; die Meldung kann den Zielpfad enthalten. Wird die Lieferung gelöscht, kommt pro Reaktion eine Zeile `Deletion` dazu, mit dem Benutzer, der gelöscht hat, und dem Zustand der Reaktion danach: `Done` hatte die Lieferung schon weitergegeben, `Error` war schon endgültig gescheitert, `Deleted` endete mit der Löschung. Setzt ein Admin Reaktionen zurück, die aufgegeben hatten, kommt pro Reaktion eine Zeile `Reset` dazu, mit dem Admin und dem Zustand `Pending`; die Versuche danach zählen wieder ab 1, die Zeilen davor behalten ihren Ausgang. Die Spalte `UserId` nennt nur, wer ein Ereignis ausgelöst hat: Einen Zustellversuch führt die Applikation aus, dort bleibt sie leer. Wer die Lieferung deklariert hat, steht an der Lieferung (`DeclaringUserId` oder `DeclaringClientId`), wer den Lauf gestartet hat, am Lauf.

## Schreibzeitpunkte und Verlässlichkeit

- **Beim Job-Start, hart:** Kann der Startdatensatz nicht geschrieben werden, wird der Job nicht angenommen und der Aufrufer erhält einen Fehler statt einer Bestätigung. Eine angenommene Lieferung ohne Protokoll gibt es nicht.
- **Danach weich:** Schreibfehler während des Laufs brechen die Pipeline nicht ab, sie werden als Warnung geloggt.
- Alle Schritt-Zeilen werden mit dem Startdatensatz als `Pending` angelegt, beim Erreichen auf `Running` gesetzt und beim Abschluss aktualisiert. Ein unterbrochener Lauf zeigt so das volle Bild: erledigte Schritte tragen ihren Endzustand, der laufende steht auf `Running`, und ein Schritt, der `Pending` geblieben ist, wurde nie erreicht.
- Die Eingaben eines Schritts werden geschrieben, sobald er sie gebunden hat und bevor sein Prozessor läuft. Ein unterbrochener Lauf zeigt so auch, womit der laufende Schritt arbeitete.
- **Ein Lauf ohne Terminal-Status bedeutet "Ausgang unbekannt":** die Instanz ist während des Laufs gestorben (Restart-Opfer). Ein sauberer Stopp wird als `Cancelled` mit Grund `host shutdown` festgehalten, ein harter Abbruch hinterlässt keinen Terminal-Status. Beides ist zählbar und macht messbar, wie oft Neustarts laufende Jobs treffen (Re-Evaluations-Trigger von ADR 0010).
- Ein Zustellversuch wird angelegt, sobald eine Instanz ihn beginnt, mit dem Zustand `Running`, und bei seiner Rückmeldung abgeschlossen. Steht ein Versuch ohne Ende auf `Running`, während keine Instanz ihn mehr ausführt, ist sein Ausgang unbekannt, wie bei einem Lauf ohne Terminal-Status. So bleibt auch der letzte Versuch einer Reaktion stehen, die aufgegeben wurde, weil er sich nie zurückmeldete: Das Aufgeben selbst hält das Protokoll nicht fest. Ein leerer Zustand heisst, dass der Versuch seine Reaktion bei der Rückmeldung nicht mehr hielt; was er getan hat, sagt die Meldung.

## Wo ist der Preflight?

Die Weboberfläche zeigt die Vorbereitung (Vollständigkeitsprüfung, Virenscan) als eigenen Schritt an, bei der Abgabe wie auf der Seite eines Laufs; das ist reine Darstellung. Im Protokoll ist der Preflight bewusst **keine** Schritt-Zeile, denn er steht in keiner Pipeline-Definition (und ein Kunde darf einen echten Schritt `preflight` nennen). Seine Ergebnisse liegen am Lauf selbst: der Scan-Ausgang mit Details, die Datei-Hashes im Manifest, und bei einer Ablehnung der Terminal-Status `Failed` mit dem Grund (`IncompleteUpload: ...`, `SizeExceeded: ...`, `ThreatDetected: ...`).

Daraus lässt sich der Preflight-Zustand eines Laufs ableiten:

- `Failed` und alle Schritte in `Pending`: im Preflight gescheitert, der Grund steht in `FailureReason`.
- Kein Terminal-Status, alle Schritte `Pending`, `ScanState = NotScanned`: durch einen Neustart im Preflight unterbrochen.
- Grenze ohne ClamAV: dort bleibt `ScanState` immer `NotScanned`, dann ist "im Preflight unterbrochen" von "kurz danach in der Warteschlange unterbrochen" nicht unterscheidbar. Beides ist ein Neustart, bevor der erste Schritt lief, und zählt gleich.

## Der Definitions-Snapshot

Pipeline-Definitionen sind nicht versioniert: nach einer Änderung der YAML existiert der alte Stand nicht mehr. Deshalb hält jeder Lauf die Fassung fest, auf der er ausgeführt wurde, als JSON-Dokument (`jsonb`-Spalte `Definition`), aufgebaut wie eine minimale Definitionsdatei für genau diese eine Pipeline:

- `pipelines`: die gelaufene Pipeline mit allen Schritten, Inputs, Output-Actions und Conditions,
- `processes`: nur die von diesen Schritten referenzierten Katalogeinträge,
- `process_configs`: die wirksame Basis-Konfiguration (`Pipeline:ProcessConfigs`) dieser Implementierungen. Sie steht nicht in der YAML, entscheidet aber mit über das Verhalten, etwa `modelDirs`, also gegen welche Modell-Repositories eine INTERLIS-Validierung prüft.

Nicht erfasst sind die Inhalte von `${file()}`-Ressourcen (welchen Stand ein Schritt bekam, zeigt der SHA-256 in seinen Eingaben), die Modell-Repositories hinter `modelDirs` und die Version der ilitools-wrapper. Der Snapshot beantwortet "auf welcher Grundlage lief das", er macht den Lauf nicht wiederholbar.

## Abfrage

Administratoren finden das Protokoll im Admin-Portal unter «Verarbeitungen». Die Liste zeigt die Läufe der Installation, neueste zuerst, mit Urheber, Mandat, Zustand, Lieferung und Dauer. Ist die maschinelle Anlieferung eingerichtet, unterscheidet ein Symbol beim Urheber Benutzer:innen von Maschinen-Clients, und eine Spalte zählt die Versuche einer Lieferung. Die Liste filtert nach Zeitraum, Urheber, Mandat und Zustand, bei Urheber, Mandat und Zustand auch nach mehreren zugleich; «Nicht abgeschlossen» findet die Läufe ohne Terminal-Status. Eine Job-ID im Suchfeld öffnet ihren Lauf direkt, auch wenn nie eine Lieferung daraus wurde, und die Seite einer Lieferung verlinkt ihren Lauf. Die Seite eines Laufs fasst ihn zusammen. Hatte seine Lieferung mehrere Versuche, listet sie darüber alle Versuche mit Start, Zustand und Lieferung, und ein Klick auf einen Versuch öffnet seine Seite. Unter der Zusammenfassung zeigt die Seite in Tabs den Ablauf, die Ereignisse der Reaktionen und die Pipeline, wie sie beim Start konfiguriert war, lesbar oder als JSON. Der Ablauf zeigt wie bei der Abgabe zuerst die Vorbereitung mit den hochgeladenen Dateien und dem Virenscan, dann die Schritte. Endete der Lauf ausserhalb der Schritte, etwa im Timeout, steht der Grund darüber.

Was aus einem Lauf wurde, steht nicht im Protokoll. Die Ansicht leitet es beim Lesen ab: aus dem Protokoll, aus den Lieferungen und daraus, ob der Job in der antwortenden Instanz noch läuft. Ob eine Lieferung entstehen konnte, entscheiden zwei unabhängige Fragen: ob die Daten sie zuliessen, der Lauf also in `Success` oder `Warning` endete, und ob, wer ihn startete, auf dem Mandat abgeben durfte (`Declarability`).

- Die Id der Lieferung, die für den Lauf deklariert wurde, mit dem Zustand ihrer Weitergabe; «(gelöscht)», wenn sie wieder gelöscht wurde.
- «nicht möglich»: Die Daten liessen keine Lieferung zu, gleich wer den Lauf startete. Der Zustand nennt die Schritte, die die Lieferung einschränkten, scheiterten oder abgebrochen wurden, oder die Vorbereitung, wenn der Preflight scheiterte. Endete der Lauf ausserhalb der Schritte, nennt er keinen.
- «nicht berechtigt»: Die Daten liessen eine Lieferung zu, aber wer den Lauf startete, durfte nicht abgeben, etwa nach einem anonymen Start auf einem öffentlichen Mandat.
- «nicht abgegeben»: Die Daten liessen eine Lieferung zu, und wer den Lauf startete, durfte abgeben, deklariert wurde aber keine. Das umfasst auch eine Abgabe im Web, die abgewiesen und nicht wiederholt wurde; den Grund sah die Person im Dialog, das Protokoll hält ihn nicht fest.
- «gescheitert»: Die Daten liessen eine Lieferung zu, und wer den Lauf startete, durfte abgeben, aber die Deklaration, die geopilot bei einer maschinellen Lieferung selbst vornimmt, wurde abgewiesen. Weil dabei niemand zuschaut, hält das Protokoll den Grund fest, die Seite des Laufs nennt ihn (`DeclarationFailure`).
- Keine Angabe: Der Lauf läuft noch, oder sein Ausgang ist unbekannt.

Bei einem Lauf ohne `Declarability` gilt nur ein anonymer Start als nicht berechtigt; die Seite des Laufs weist darauf hin.

Dieselben Angaben liefert die API:

- `GET api/v1/pipelinerun`: die Läufe seitenweise, neueste zuerst, gefiltert nach Zeitraum (`from`, `to`), Mandaten (`mandateIds`), Urhebern (`userIds`, `machineClientIds`) und Zuständen (`states`). Ein Lauf passt zu einem Filter mit mehreren Werten, wenn er zu einem davon passt. Die Versuche einer Lieferung bilden eine Gruppe und zählen beim Blättern als eine Zeile; ein Lauf ohne `ClientReference` ist eine Gruppe für sich. Eine Gruppe erscheint, wenn einer ihrer Versuche im Zeitraum liegt, der Zustand gilt ihrem neuesten Versuch, und die früheren Versuche kommen mit, auch die ausserhalb des Zeitraums.
- `GET api/v1/pipelinerun/{jobId}`: der Lauf mit Manifest, Schritten samt ihren Eingaben (`inputs`), Condition-Auswertungen, Artefaktnamen, der Deklarierbarkeit (`declarability`), dem Grund eines Scheiterns, das kein Schritt nennt (`failureReason`), dem Grund einer gescheiterten Deklaration (`declarationFailure`) und den Zustellversuchen der Reaktionen (`reactionEvents`), dazu Urheber und Mandat mit Namen (`declarer`, `mandate`), was aus dem Lauf wurde (`outcome`, mit `preflightFailed`, wenn er im Preflight scheiterte), die Zustände der Weitergabe und der Benachrichtigungen seiner Lieferung (`forwardingState`, `notificationState`) und die Versuche der Lieferung, neueste zuerst und der Lauf selbst darunter (`attempts`).
- `GET api/v1/pipelinerun/{jobId}/definition`: der Definitions-Snapshot als JSON.

Für Auswertungen über mehrere Läufe ist SQL das Werkzeug. Beispiele:

```sql
-- Welche Pipeline-Fassung lief für Delivery X, welche Schritte, warum?
SELECT r."PipelineId", s."StepId", s."State", s."ErrorMessage", c."ConditionId", c."Matched", c."EvaluatedValues"
FROM "Deliveries" d
JOIN "PipelineRuns" r ON r."JobId" = d."JobId"
JOIN "PipelineRunSteps" s ON s."PipelineRunId" = r."Id"
LEFT JOIN "PipelineRunConditions" c ON c."PipelineRunStepId" = s."Id"
WHERE d."Id" = 42
ORDER BY s."Order";

-- Womit arbeiteten die Schritte von Delivery X?
SELECT s."StepId", i."Parameter", i."Origin", i."Reference", i."Kind", i."Value", i."FileName", i."Size", i."Sha256"
FROM "Deliveries" d
JOIN "PipelineRuns" r ON r."JobId" = d."JobId"
JOIN "PipelineRunSteps" s ON s."PipelineRunId" = r."Id"
JOIN "PipelineRunInputs" i ON i."PipelineRunStepId" = s."Id"
WHERE d."Id" = 42
ORDER BY s."Order", i."Id";

-- Welche Läufe bekamen eine Ressource in welchem Stand?
SELECT r."JobId", r."StartedAt", s."StepId", i."Sha256"
FROM "PipelineRunInputs" i
JOIN "PipelineRunSteps" s ON s."Id" = i."PipelineRunStepId"
JOIN "PipelineRuns" r ON r."Id" = s."PipelineRunId"
WHERE i."Origin" IN ('Resource', 'Configuration') AND i."Reference" = 'dmav/refdata_mapping.xtf'
ORDER BY r."StartedAt";

-- Welche Läufe nutzten eine bestimmte Prozessor-Implementierung?
SELECT r."JobId", r."StartedAt"
FROM "PipelineRuns" r
WHERE r."Definition" @> '{"processes": [{"implementation": "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess"}]}';

-- Wie viele Läufe blieben ohne Terminal-Status (Restart-Opfer)?
SELECT count(*) FROM "PipelineRuns" WHERE "TerminalState" IS NULL AND "StartedAt" < now() - interval '1 day';

-- Lieferbare Läufe ohne Lieferung: nicht deklarierbar, an der Deklaration gescheitert oder nie abgegeben?
SELECT r."JobId", r."StartedAt", r."Declarability", r."DeclarationFailure"
FROM "PipelineRuns" r
WHERE r."TerminalState" IN ('Success', 'Warning')
  AND NOT EXISTS (SELECT 1 FROM "Deliveries" d WHERE d."JobId" = r."JobId");

-- Läufe gruppiert nach identischer Definitions-Fassung.
SELECT "PipelineId", "Definition", count(*), min("StartedAt"), max("StartedAt")
FROM "PipelineRuns"
GROUP BY "PipelineId", "Definition";

-- Alle Versuche einer maschinellen Lieferung, ältester zuerst.
SELECT "JobId", "StartedAt", "TerminalState"
FROM "PipelineRuns"
WHERE "ClientReference" = 'LIEF-2026-0815' AND "MachineClientId" = 3 AND "MandateId" = 7
ORDER BY "StartedAt";

-- Wie wurde Delivery X weitergegeben?
SELECT e."ReactionId", e."Attempt", e."OccurredAt", e."FinishedAt", e."State", e."Message" ->> 'de' AS "Message"
FROM "PipelineRunReactionEvents" e
WHERE e."DeliveryId" = 42
ORDER BY e."OccurredAt", e."Id";
```

## Aufbewahrung

`Processing:ProtocolRetention` (Standard: 3650 Tage) bestimmt, wie lange Protokoll-Datensätze aufbewahrt werden, unabhängig von der viel kürzeren `Processing:JobRetention`. Der Aufräumdienst löscht ältere Läufe samt Kind-Datensätzen. Ist der Schlüssel nicht gesetzt, wird nie gelöscht: ein fehlender Konfigurationswert kann das Protokoll nicht stillschweigend leeren. Die Frist ist mit den Datenschutz-Anforderungen des Betreibers abzustimmen, das Protokoll enthält Benutzerreferenzen und Dateinamen, und mit der `ClientReference` einen Freitext, den ein Client für seine Lieferungen wählt.

## Dateien

Das Protokoll hält **keine** Dateien, nur Metadaten und eine credential-freie Referenz: den Ablageort des Upload-Storage (`UploadStorageLocation`) plus den Storage-Key pro Datei. Wie lange die Dateien selbst existieren, bestimmen die bestehenden Regeln: Uploads werden gelöscht, sobald ihr Lauf endet, ausser bei einem abgebrochenen Lauf, der sie bis zum Ablauf der Upload-Session behält. Downloads und Visualisierungen verschwinden nach ihrer kurzen Retention, Lieferdateien eines Laufs, den jemand deklarieren kann, warten bis zur Einreichung unter `assets/staging/<jobId>` und verschwinden mit dem Job nach der `JobRetention`, wenn keine Lieferung deklariert wird; alle drei Fristen zählen ab dem Ende des Laufs; Lieferdateien einer eingereichten Lieferung bleiben als Assets erhalten (erreichbar über den Join `Deliveries.JobId = PipelineRuns.JobId`).

Der SHA-256 im Manifest beweist die Identität einer Datei, wenn sie erneut vorgelegt wird. Sollen die referenzierten Uploads selbst über die geopilot-Fristen hinaus verfügbar bleiben, empfiehlt sich Soft Delete oder Versioning auf dem Storage-Container: dann zeigt die Referenz auch nach dem Löschen durch geopilot auf wiederherstellbare Blobs. Das ist Hosting-Konfiguration und liegt beim Betreiber.
