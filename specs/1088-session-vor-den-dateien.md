# Maschinelle Lieferung verliert Dateien, wenn das Aufräumen während der Übertragung läuft (#1088)

## Umsetzung

Die Multipart-Route legt ihre Upload-Session an, bevor sie den Body liest, wie die Initiierung in `UploadOrchestrationService.InitiateUploadAsync`: erst die Kapazität prüfen, dann die Session anlegen. Weil der Request keine Dateien anmeldet, beginnt die Session mit leerer Dateiliste. Der Sweep entfernt eine Session ohne Dateien erst, wenn sie älter ist als zweimal `Upload:UploadUrlExpiryMinutes`, wie nach der Initiierung. Ohne Dateien ist sie hier nur, solange die Route die Felder liest und das Mandat prüft. `CreateUpload` ist die erste Anweisung im `try`, damit das bestehende `finally` die Session bei jedem Fehler freigibt, auch wenn noch keine Datei angekommen ist.

Nach der letzten Datei setzt die Route die Dateiliste der Session, bevor sie die Verarbeitung startet. Dafür bekommt `IUploadStore` die Methode `SetFiles`, die die Dateiliste einer bestehenden Session ersetzt. Kennt der Store die Session nicht mehr, wirft sie, und das `finally` gibt frei, was noch liegt. Solange die Liste leer ist, nimmt der `DirectUploadEndpoint` für diese Session keine Datei an.

Die Session zählt damit ab Beginn des Requests gegen `Upload:MaxActiveJobs`, nicht erst nach der letzten Datei. So zählen laufende Übertragungen mit, wie Uploads nach der Initiierung, und zwar für weitere Multipart-Requests wie für die Initiierung über `POST api/v2/upload`. An der Grenze lehnen beide deshalb ab, wo sie heute noch durchkämen. Route, Formular und Antworten bleiben sonst gleich.

Liegen die Upload-Sessions bei der Umsetzung schon in der Datenbank, gilt dasselbe dort: die Session zu Beginn schreiben, ihre Dateien nach der letzten Datei.

CHANGELOG unter Fixed: Eine maschinelle Lieferung, die ihre Dateien im Request mitschickt, verliert keine Dateien mehr, wenn während der Übertragung aufgeräumt wird. Laufende Übertragungen zählen ab ihrem Beginn gegen `Upload:MaxActiveJobs`.

## Prüfung

- `SubmissionControllerTest`: Ein Request mit zwei Dateien, dessen Body mitten in der zweiten Datei den echten Sweep laufen lässt (`UploadCleanupService.RunCleanupAsync` auf demselben `UploadStore` und Speicher), wird angenommen, und beide Dateien liegen danach vollständig im Speicher. Die zweite Datei ist grösser als der Lesepuffer des `MultipartReader`, damit die erste beim Sweep schon geschrieben ist. Ohne den Fix löscht der Sweep die erste.
- `SubmissionControllerTest`: Ein Request, der an einem Feld scheitert, einer mit zu grosser Datei (413) und einer, dessen Body zu früh endet, hinterlassen weder Session noch Dateien. Die bestehenden Tests dazu prüfen zusätzlich die Session.
- `UploadStoreTest`: `SetFiles` ersetzt die Dateiliste einer bestehenden Session und wirft für eine unbekannte.
- Smoke-Test: ohne Object Storage, `Upload:CleanupIntervalMinutes` auf 1. Zwei Dateien, die die Pipeline des Mandats annimmt, an die Multipart-Route schicken: die erste klein, die zweite so gross, dass sie mit `curl --limit-rate` über zwei Minuten braucht. Die Rate bleibt über 240 Bytes pro Sekunde, sonst bricht Kestrel den Request ab. Der Versuch endet als `delivered`. Gegenprobe vorher mit dem heutigen Code: Derselbe Aufbau endet als `failed`. Mit nur einer Datei taugt der Test nicht, denn unter Windows kann der Sweep eine offene Datei nicht löschen.
