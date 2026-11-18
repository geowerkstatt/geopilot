# Downloads, Visualisierungen und Delivery-Assets im Object Store ablegen (#850)

Review: required. Jede Installation muss Konfigurationsschlüssel umstellen, die Betreiberdoku ändert sich, und CI ebenso. Der Zugriff auf den Upload-Speicher wird umgebaut, und Uploads zählen als sicherheitsrelevant. Ein Fehler in der Deklaration kostet Lieferungen.

Abhängig von: #1199 und #947, beide nur für PR 2. #1199 bringt den S3-Provider, den diese Spec um Schreiben und Kopieren erweitert. Von #947 übernimmt sie die Reihenfolge der Deklaration und die Regeln, nach denen das Cleanup Lieferdateien behält; hier wird beides für den Bucket übersetzt.

## Ziel

Eine Installation hat einen Object Store für alle ihre Dateien oder keinen. Mit Object Store liegen neben den Uploads auch Downloads, Visualisierungen und Lieferdateien samt Staging im Bucket, nicht auf der Disk der Instanz. Gesichert werden müssen dann nur noch die Datenbank und der Bucket. Mehrere Instanzen sehen dieselben Dateien, auch wo es kein geteiltes Volume gibt; damit sie sich vertragen, braucht es zusätzlich #954. Ohne Object Store bleibt alles wie heute auf der Disk, und das bleibt ein vollwertiger Modus.

Akzeptanzkriterien:

- Ein Schalter `Storage:Backend` entscheidet für alle Dateien: Mit `Cloud` liegen Uploads, Downloads, Visualisierungen und Lieferdateien samt Staging in einem Bucket, mit Azure Blob Storage oder S3. Mit `Local` liegen sie auf der Disk wie heute im Modus `Direct`.
- Die bisherigen Schlüssel `Upload:Backend`, `Upload:Cloud` und `Upload:Direct` stoppen den Start mit einer Meldung, die die neuen Schlüssel nennt.
- Die API-Antworten und die Autorisierung ändern sich nicht.
- Ein Neustart verliert keine Lieferdatei. Für Downloads und Visualisierungen gelten dieselben Cleanup-Regeln wie auf der Disk.
- Ein Präfix mit Lieferdateien entsteht erst, wenn die Lieferung gespeichert ist. Das Cleanup löscht es nur, wenn die Datenbank die Lieferung als gelöscht führt.
- Nach Ablauf der Retention sind die Objekte eines Jobs entfernt, ohne verwaiste Präfixe.
- Im Modus `Cloud` verlangen Startprüfung und Health Check keines der Datenverzeichnisse ausser `PipelineDirectory` und `ResourcesDirectory`, sondern einen erreichbaren Bucket.

## Umsetzung

Zwei PRs. PR 1 verschiebt die Schlüssel und steht für sich; kommt er vor #1199, legt #1199 seine Schlüssel gleich unter `Storage:Cloud` an. PR 2 legt die Artefakte in den Bucket. Ein Release mit PR 1 verlangt von jedem Hosting, die Schlüssel im selben Schritt umzustellen, in dem es das Image aktualisiert, sonst startet geopilot nicht.

**Schlüssel (PR 1).** Was festlegt, wo Dateien liegen, wandert in den bestehenden Abschnitt `Storage:` (`FileAccessOptions`):

| Bisher | Neu |
| --- | --- |
| `Upload:Backend` (`Cloud`, `Direct`) | `Storage:Backend` (`Cloud`, `Local`), Standard weiterhin `Cloud` |
| `Upload:Cloud:*` | `Storage:Cloud:*`, dieselben Unterschlüssel |
| `Upload:Direct:Directory` | `Storage:UploadDirectory`, neben den anderen Verzeichnissen |

Unter `Upload:` bleibt, was Uploads begrenzt und aufräumt (`MaxFileSizeMB` bis `RateLimitWindowMinutes`). Die alten Schlüssel weist der Start ab wie heute den Abschnitt `CloudStorage` (`ConfigurationBuilderExtensions`), mit der Tabelle oben in der Meldung. Weil das Upload-Verzeichnis jetzt ein Verzeichnis unter `Storage:` ist, prüfen `DataDirectories`, die Überlappungsprüfung und `StorageHealthCheck` es mit den anderen; `UploadDirectDirectoryValidator` und `UploadDirectoryHealthCheck` gehen darin auf. Die maschinelle Lieferung bleibt, wie sie ist: JSON-Route bei `Cloud`, Multipart-Route bei `Local`.

**Ein Bucket (PR 2).** Im Modus `Cloud` liegen alle Dateien einer Installation in dem einen Bucket aus `Storage:Cloud`, unter Präfixen nach dem heutigen Verzeichnisbaum: `uploads/` wie heute, `downloads/<jobId>/<Datei>`, `visualizations/<jobId>/<Datei>`, `assets/<jobId>/<Datei>` und das Staging unter `assets/staging/<jobId>/<Datei>`. Der Upload-Sweep löscht heute alles ausserhalb von `uploads/`; das entfällt, er räumt nur noch in `uploads/` auf. Einen Bucket, den sich zwei Installationen teilen, erkennt geopilot nicht; das README schliesst ihn aus, weil das Cleanup der einen die Präfixe der anderen für verwaist hielte. `Storage:DownloadDirectory`, `VisualizationDirectory`, `AssetsDirectory` und `UploadDirectory` werden im Modus `Cloud` nicht gelesen und sind nicht nötig, `PipelineDirectory` und `ResourcesDirectory` bleiben Pflicht. Damit entfällt der `shortcut:` in `DataDirectories.Local`.

**Provider.** `IUploadStorage` kann schon lesen, nach Präfix auflisten und löschen. Diese Operationen kommen in eine neutrale Schnittstelle für Objekte nach Schlüssel, ergänzt um Schreiben aus einem Stream und serverseitiges Kopieren. Azure und S3 implementieren sie je einmal, und `IUploadStorage` behält nur, was allein Uploads brauchen (die Upload-URL). Wie die Klassen geschnitten werden, entscheidet die Umsetzung. Bedingung ist, dass Konfiguration und Eigenheiten eines Providers an genau einer Stelle stehen.

**Stores.** `IJobFileStore` wird asynchron: lesen als Stream, schreiben aus einem Stream (statt einen beschreibbaren Stream zu liefern), prüfen, ob es eine Datei gibt, auflisten und pro Job löschen. `GetPath` entfällt, das nutzen nur Tests. Zu jeder Art kommt neben dem physischen Store einer im Bucket. Alles geht über die Stores, auch was heute direkt auf Verzeichnisse greift: `AssetHandler.DownloadAssetAsync` liest eine Lieferdatei heute ganz in den Speicher und streamt sie neu bis in die Antwort. Dazu kommen `AssetHandler.DeleteJobAssets` und `ProcessingJobCleanupService`. Ausgeliefert wird weiterhin durch die API, mit den heutigen Prüfungen.

**Deklaration.** Die Reihenfolge folgt #947: Die Dateien werden im Staging gehasht, dann wird die Lieferung gespeichert, und erst danach kommen die Dateien an ihren Platz. Im Bucket heisst das: jede Datei serverseitig von `assets/staging/<jobId>/` nach `assets/<jobId>/` kopieren und danach das Staging-Präfix löschen. Bricht das dazwischen ab, liegen Dateien an beiden Orten. Der nächste Cleanup-Lauf kopiert, was fehlt, und löscht dann das Staging, so wie er nach #947 ein liegengebliebenes Staging an seinen Platz schiebt. Kopieren ist wiederholbar, ein zweiter Durchgang schadet nicht. Es muss auch grosse Objekte können (Azure kopiert sie asynchron, S3 über 5 GB nur mehrteilig), denn erzeugte Dateien wie ZIP-Pakete fallen nicht unter die Grenzen der Uploads.

Eine Lieferdatei, die noch nicht unter `assets/<jobId>/` liegt, liest der Store aus dem Staging. Sonst scheiterte eine Reaktion, die gleich nach dem Speichern fällig wird, während des Kopierens, und nach einem Abbruch bis zum nächsten Cleanup-Lauf; jeder Fehlversuch kostete einen ihrer Versuche. Dasselbe gilt für den Download über den STAC-Asset-Link. Gelesen werden so nur Dateien gespeicherter Lieferungen, nach denen ein Leser fragt; ein Staging ohne Lieferung bleibt unerreichbar. Ein gemeinsames Präfix für Staging und Lieferung scheidet aus: Das Cleanup könnte dann eine nie deklarierte Lieferung nicht von einer unterscheiden, die nach dem letzten Backup deklariert wurde.

**Cleanup.** `ProcessingJobCleanupService` holt die Job-Ids über die Stores statt über Verzeichnisse und löscht pro Job über den Store, im Bucket also ein Präfix. Welche Jobs wann gehen, bestimmen weiterhin #947 und #954; diese Spec ändert nur den Weg dorthin. Beide Tickets ändern `ProcessingJobCleanupService` und `AssetHandler`. Was nach dem anderen kommt, baut auf dessen Stand auf. `Processing:DownloadRetention`, `VisualizationRetention` und `JobRetention` gelten unverändert. Lifecycle-Regeln des Providers werden nicht genutzt: Der Modus `Local` braucht dieselbe Logik ohnehin, und S3-kompatible Anbieter bieten sie nicht durchgehend an.

**Health.** Im Modus `Cloud` prüft der Start, ob der Bucket erreichbar und beschreibbar ist (Probe schreiben und löschen, wie heute bei den Verzeichnissen), und bricht sonst mit `Storage:Cloud` in der Meldung ab. `StorageHealthCheck` prüft bei jeder Abfrage, ob der Bucket erreichbar ist, und bleibt beim Ready-Endpunkt. Die Antwort nennt nur den Konfigurationsschlüssel, Endpunkt und Fehler gehen ins Log, weil die Health-Endpunkte anonym antworten.

**Update und Umstieg.** Disk und Bucket werden nicht parallel gelesen. Das README beschreibt drei Fälle:

- Jede Installation stellt mit dem Update die Schlüssel nach der Tabelle um.
- Eine Installation mit `Cloud` kopiert beim Update von PR 2 zusätzlich ihre Lieferdateien: geopilot stoppen, `Storage:AssetsDirectory` samt `staging` nach `assets/` im Bucket kopieren, starten. Das Staging gehört dazu, weil eine gespeicherte Lieferung nach einem Absturz noch dort liegen kann; was keiner Lieferung gehört, räumt das Cleanup nach dem Start ab.
- Eine Installation, die von `Local` auf `Cloud` wechselt, kopiert ebenso und stellt `Storage:Backend` und `Storage:Cloud` um. Uploads, Downloads und Visualisierungen werden nicht kopiert. Sie sind flüchtig, und ihre Jobs enden mit dem Stopp; ein Upload, der zu dem Zeitpunkt auf einen Start wartet, muss neu hochgeladen werden.

**Doku.**

- README, Betrieb: die Schlüssel unter `Storage:`, ein Bucket pro Installation, die drei Fälle aus dem Update, dass mehr als eine Instanz #954 voraussetzt, und was gesichert werden muss: bei `Cloud` die Datenbank und der Bucket, bei `Local` die Datenbank und die Verzeichnisse.
- Alle Stellen, die die alten Schlüssel nennen: `docker-compose.yml`, das Cypress-Overlay `docker-compose.cypress.yml` (CI), `docs/pipeline/Kanaele/ablage.md`, `scripts/README.md`.
- CHANGELOG unter Changed: die Tabelle der Schlüssel, dass die alten den Start stoppen, und der Hinweis zum Kopieren für Installationen mit `Cloud`. Unter Added: Downloads, Visualisierungen und gelieferte Dateien liegen bei `Cloud` im Bucket der Uploads.

## Nicht im Umfang

- Mischformen, etwa Uploads im Bucket und Lieferdateien auf der Disk.
- Die Hosting-Konfigurationen selbst. Sie liegen ausserhalb dieses Repos und werden dort mit dem Update umgestellt.
- Das Arbeitsverzeichnis der Pipeline und die Resource-Files bleiben lokal.
- Presigned URLs für Downloads: Sie verlagern die Autorisierung auf den Besitz der URL und sind eigens zu entscheiden.
- Paralleles Lesen aus Disk und Bucket, ein Werkzeug für den Umzug und ein Umzug zwischen Providern.
- Versionierung, Replikation und Lifecycle-Regeln des Buckets sind Sache des Betriebs.
- Der Zustand der Jobs über Instanzen hinweg (#954) und die Regeln, wann das Cleanup was löscht (#947, #954). Bis #954 hält das Cleanup jeder Instanz die Jobs der anderen für verwaist, im Bucket wie auf einem geteilten Volume.

## Prüfung

- Start: Jeder der alten Schlüssel stoppt den Start mit der Tabelle in der Meldung (`LegacyConfigurationGuardTest`). `Cloud` ohne `Storage:Cloud` startet nicht, `Cloud` ohne die vier Verzeichnisse startet, ein unerreichbarer Bucket stoppt den Start mit dem Schlüssel in der Meldung. Bei `Local` stoppt ein Upload-Verzeichnis, das ein anderes überlappt, den Start wie heute.
- Store-Tests, dieselben Fälle gegen Disk, Azurite und den S3-Server aus #1199 in CI: schreiben, lesen, prüfen, auflisten, pro Job löschen, kopieren.
- Deklaration im Bucket: Scheitert das Speichern, entsteht nichts unter `assets/<jobId>/`. Bricht das Kopieren nach dem Speichern ab, vervollständigt der nächste Cleanup-Lauf die Lieferung und löscht das Staging. Die Tabellentests des Cleanups aus #947 laufen auch gegen den Bucket. Eine Lieferdatei, die erst im Staging liegt, lässt sich für eine gespeicherte Lieferung lesen.
- Der Upload-Sweep lässt Objekte ausserhalb von `uploads/` stehen.
- Smoke-Test mit `Cloud`, je mit Azurite und dem S3-Server: hochladen im Wizard und über die maschinelle Lieferung, eine Pipeline erzeugt einen Download, eine Visualisierung und eine Lieferung, die per Ablage-Reaktion weitergeht. Nach einem Neustart ist die Lieferdatei über den STAC-Asset-Link abrufbar. Dazu einmal das Update nach README: Lieferungen auf der Disk kopieren, starten, eine alte Lieferung abrufen. Mit `Local` laufen dieselben Abläufe wie heute, die maschinelle Lieferung über die Multipart-Route.
- CI ist grün, auch der Abgleich der generierten Typen: Der API-Vertrag ändert sich nicht.
