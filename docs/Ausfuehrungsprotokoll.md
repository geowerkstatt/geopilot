# Ausführungsprotokoll

geopilot hält zu jedem Verarbeitungsjob ein dauerhaftes Ausführungsprotokoll in der Datenbank fest: was lief, auf welcher Fassung der Pipeline-Definition, für wen, und wie es endete. Das Protokoll ist der Prüfnachweis des Lieferprozesses. Es überlebt Neustarts und die Job-Retention, und zwar ausdrücklich auch für abgelehnte und abgebrochene Läufe, die sonst keine Spur hinterlassen.

Das Protokoll wird nur geschrieben. Kein Ausführungspfad liest es, um Entscheidungen zu treffen: es beschreibt die Vergangenheit, es macht sie nicht wieder ausführbar.

## Was aufgezeichnet wird

Pro Job entsteht beim Start ein Datensatz (`PipelineRuns`) mit:

- der gelaufenen Pipeline und einem **Definitions-Snapshot** (siehe unten),
- Mandat und Urheber, entweder der Benutzer oder der Maschinen-Client (`UserId` oder `MachineClientId`, nie beide zugleich, was die Datenbank erzwingt; beide leer, wenn der Job anonym auf einem öffentlichen Mandat gestartet wurde; anonym darf prozessiert, aber nicht geliefert werden, und wer die Lieferung deklariert hat, steht an der Lieferung selbst) sowie der Art des Clients (`WebClient`, `ApiClient`, `Unknown`), klassifiziert aus dem Request und nie als roher Header gespeichert. Die Art des Clients sagt, auf welchem Weg der Job gestartet wurde, nicht von wem: eine Person, die die API direkt aufruft, etwa aus Swagger, ist ebenso `ApiClient` wie ein Maschinen-Client, und wer es war, steht in den beiden Id-Spalten,
- dem Upload-Manifest (`PipelineRunFiles`): Dateiname, Storage-Key, deklarierte Grösse, und nach dem Virenscan der SHA-256 jeder Datei,
- dem Resultat des Virenscans (`Clean`, `ThreatDetected` mit Details, oder `NotScanned` wenn die Prüfung deaktiviert ist),
- der Applikationsversion und allen Zeitstempeln in UTC.

Pro Schritt (`PipelineRunSteps`) werden festgehalten: Status, Beginn und Ende, die Prozessor-Implementierung samt Assembly und Version, Fehlermeldung bei Abbruch, Statusmeldung, sowie die **Auswertung jeder geprüften Condition** (`PipelineRunConditions`): Ausdruck, optionale stabile Kennung (`id` in der Definition), die referenzierten Werte und ob sie zutraf. Auch nicht zutreffende Auswertungen werden festgehalten, damit "geprüft und nicht zugetroffen" von "nie geprüft" unterscheidbar bleibt. Die Namen der erzeugten Artefakte (Downloads, Visualisierungen, Lieferdateien) stehen in `PipelineRunArtifacts`.

Wird die Lieferung eines Laufs weitergegeben (siehe [Weitergabe nach dem Abschluss](pipeline/Pipelines.md#weitergabe-nach-dem-abschluss)), steht jeder Zustellversuch ihrer Reaktionen in `PipelineRunReactionEvents`: die Lieferung, die Id der Reaktion im Satz, ob die Reaktion die Lieferung weitergibt oder begleitet, der Kanal, die Nummer des Versuchs, Beginn und Ende, der Zustand der Reaktion danach und die Meldung des Kanals. Personendaten des Lieferanten und Werte des Mandats stehen nicht darin; die Meldung kann den Zielpfad enthalten. Wird die Lieferung gelöscht, kommt pro Reaktion eine Zeile `Deletion` dazu, mit dem Benutzer, der gelöscht hat, und dem Zustand der Reaktion danach: `Done` hatte die Lieferung schon weitergegeben, `Error` war schon endgültig gescheitert, `Deleted` endete mit der Löschung. Setzt ein Admin Reaktionen zurück, die aufgegeben hatten, kommt pro Reaktion eine Zeile `Reset` dazu, mit dem Admin und dem Zustand `Pending`; die Versuche danach zählen wieder ab 1, die Zeilen davor behalten ihren Ausgang. Die Spalte `UserId` nennt nur, wer ein Ereignis ausgelöst hat: Einen Zustellversuch führt die Applikation aus, dort bleibt sie leer. Wer die Lieferung deklariert hat, steht an der Lieferung (`DeclaringUserId` oder `DeclaringClientId`), wer den Lauf gestartet hat, am Lauf.

## Schreibzeitpunkte und Verlässlichkeit

- **Beim Job-Start, hart:** Kann der Startdatensatz nicht geschrieben werden, wird der Job nicht angenommen und der Aufrufer erhält einen Fehler statt einer Bestätigung. Eine angenommene Lieferung ohne Protokoll gibt es nicht.
- **Danach weich:** Schreibfehler während des Laufs brechen die Pipeline nicht ab, sie werden als Warnung geloggt.
- Alle Schritt-Zeilen werden mit dem Startdatensatz als `Pending` angelegt, beim Erreichen auf `Running` gesetzt und beim Abschluss aktualisiert. Ein unterbrochener Lauf zeigt so das volle Bild: erledigte Schritte tragen ihren Endzustand, der laufende steht auf `Running`, und ein Schritt, der `Pending` geblieben ist, wurde nie erreicht.
- **Ein Lauf ohne Terminal-Status bedeutet "Ausgang unbekannt":** die Instanz ist während des Laufs gestorben (Restart-Opfer). Ein sauberer Stopp wird als `Cancelled` mit Grund `host shutdown` festgehalten, ein harter Abbruch hinterlässt keinen Terminal-Status. Beides ist zählbar und macht messbar, wie oft Neustarts laufende Jobs treffen (Re-Evaluations-Trigger von ADR 0010).
- Ein Zustellversuch wird angelegt, sobald eine Instanz ihn beginnt, mit dem Zustand `Running`, und bei seiner Rückmeldung abgeschlossen. Steht ein Versuch ohne Ende auf `Running`, während keine Instanz ihn mehr ausführt, ist sein Ausgang unbekannt, wie bei einem Lauf ohne Terminal-Status. So bleibt auch der letzte Versuch einer Reaktion stehen, die aufgegeben wurde, weil er sich nie zurückmeldete: Das Aufgeben selbst hält das Protokoll nicht fest. Ein leerer Zustand heisst, dass der Versuch seine Reaktion bei der Rückmeldung nicht mehr hielt; was er getan hat, sagt die Meldung.

## Wo ist der Preflight?

Die Weboberfläche zeigt die Vorbereitung (Vollständigkeitsprüfung, Virenscan) als eigenen Schritt an; das ist reine Darstellung. Im Protokoll ist der Preflight bewusst **keine** Schritt-Zeile, denn er steht in keiner Pipeline-Definition (und ein Kunde darf einen echten Schritt `preflight` nennen). Seine Ergebnisse liegen am Lauf selbst: der Scan-Ausgang mit Details, die Datei-Hashes im Manifest, und bei einer Ablehnung der Terminal-Status `Failed` mit dem Grund (`IncompleteUpload: ...`, `SizeExceeded: ...`, `ThreatDetected: ...`).

Daraus lässt sich der Preflight-Zustand eines Laufs ableiten:

- `Failed` und alle Schritte in `Pending`: im Preflight gescheitert, der Grund steht in `FailureReason`.
- Kein Terminal-Status, alle Schritte `Pending`, `ScanState = NotScanned`: durch einen Neustart im Preflight unterbrochen.
- Grenze ohne ClamAV: dort bleibt `ScanState` immer `NotScanned`, dann ist "im Preflight unterbrochen" von "kurz danach in der Warteschlange unterbrochen" nicht unterscheidbar. Beides ist ein Neustart, bevor der erste Schritt lief, und zählt gleich.

## Der Definitions-Snapshot

Pipeline-Definitionen sind nicht versioniert: nach einer Änderung der YAML existiert der alte Stand nicht mehr. Deshalb hält jeder Lauf die Fassung fest, auf der er ausgeführt wurde, als JSON-Dokument (`jsonb`-Spalte `Definition`), aufgebaut wie eine minimale Definitionsdatei für genau diese eine Pipeline:

- `pipelines`: die gelaufene Pipeline mit allen Schritten, Inputs, Output-Actions und Conditions,
- `processes`: nur die von diesen Schritten referenzierten Katalogeinträge,
- `process_configs`: die wirksame Basis-Konfiguration (`Pipeline:ProcessConfigs`) dieser Implementierungen. Sie steht nicht in der YAML, entscheidet aber mit über das Verhalten, etwa `modelDirs`, also gegen welche Modell-Repositories eine INTERLIS-Validierung prüft.

Nicht erfasst sind die Inhalte von `${file()}`-Ressourcen, die Modell-Repositories hinter `modelDirs` und die Version der ilitools-wrapper. Der Snapshot beantwortet "auf welcher Grundlage lief das", er macht den Lauf nicht wiederholbar.

## Abfrage

Administratoren können das Protokoll über die API lesen:

- `GET api/v1/pipelinerun/{jobId}`: der Lauf mit Manifest, Schritten, Condition-Auswertungen, Artefaktnamen und den Zustellversuchen der Reaktionen (`reactionEvents`).
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

-- Welche Läufe nutzten eine bestimmte Prozessor-Implementierung?
SELECT r."JobId", r."StartedAt"
FROM "PipelineRuns" r
WHERE r."Definition" @> '{"processes": [{"implementation": "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess"}]}';

-- Wie viele Läufe blieben ohne Terminal-Status (Restart-Opfer)?
SELECT count(*) FROM "PipelineRuns" WHERE "TerminalState" IS NULL AND "StartedAt" < now() - interval '1 day';

-- Läufe gruppiert nach identischer Definitions-Fassung.
SELECT "PipelineId", "Definition", count(*), min("StartedAt"), max("StartedAt")
FROM "PipelineRuns"
GROUP BY "PipelineId", "Definition";

-- Wie wurde Delivery X weitergegeben?
SELECT e."ReactionId", e."Attempt", e."OccurredAt", e."FinishedAt", e."State", e."Message"
FROM "PipelineRunReactionEvents" e
WHERE e."DeliveryId" = 42
ORDER BY e."OccurredAt", e."Id";
```

## Aufbewahrung

`Processing:ProtocolRetention` (Standard: 3650 Tage) bestimmt, wie lange Protokoll-Datensätze aufbewahrt werden, unabhängig von der viel kürzeren `Processing:JobRetention`. Der Aufräumdienst löscht ältere Läufe samt Kind-Datensätzen. Ist der Schlüssel nicht gesetzt, wird nie gelöscht: ein fehlender Konfigurationswert kann das Protokoll nicht stillschweigend leeren. Die Frist ist mit den Datenschutz-Anforderungen des Betreibers abzustimmen, das Protokoll enthält Benutzerreferenzen und Dateinamen.

## Dateien

Das Protokoll hält **keine** Dateien, nur Metadaten und eine credential-freie Referenz: den Ablageort des Upload-Storage (`UploadStorageLocation`) plus den Storage-Key pro Datei. Wie lange die Dateien selbst existieren, bestimmen die bestehenden Regeln: Uploads werden gelöscht, sobald ihr Lauf endet, ausser bei einem abgebrochenen Lauf, der sie bis zum Ablauf der Upload-Session behält. Downloads und Visualisierungen verschwinden nach ihrer kurzen Retention, Lieferdateien warten bis zur Einreichung unter `assets/staging/<jobId>` und verschwinden mit dem Job nach der `JobRetention`, wenn keine Lieferung deklariert wird; alle drei Fristen zählen ab dem Ende des Laufs; Lieferdateien einer eingereichten Lieferung bleiben als Assets erhalten (erreichbar über den Join `Deliveries.JobId = PipelineRuns.JobId`).

Der SHA-256 im Manifest beweist die Identität einer Datei, wenn sie erneut vorgelegt wird. Sollen die referenzierten Uploads selbst über die geopilot-Fristen hinaus verfügbar bleiben, empfiehlt sich Soft Delete oder Versioning auf dem Storage-Container: dann zeigt die Referenz auch nach dem Löschen durch geopilot auf wiederherstellbare Blobs. Das ist Hosting-Konfiguration und liegt beim Betreiber.
