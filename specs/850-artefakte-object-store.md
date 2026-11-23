# Downloads, Visualisierungen und Delivery-Assets im Object Store ablegen (#850)

Abhängig von: #1199 und #947. #1199 bringt den S3-Provider, den diese Spec um Schreiben und Kopieren erweitert. Von #947 übernimmt sie die Reihenfolge der Deklaration und die Regeln, nach denen das Cleanup Lieferdateien behält; hier wird beides für den Bucket übersetzt.

## Ziel

Downloads, Visualisierungen und Lieferdateien samt Staging können wie die Uploads im Bucket liegen statt auf der Disk der Instanz. Jede dieser Arten hat einen eigenen Schalter, weil der Betrieb sie einzeln umzieht; eine Installation kann also etwa Uploads auf der Disk und Lieferdateien im Bucket haben. Liegt alles im Bucket, müssen nur noch die Datenbank und der Bucket gesichert werden, und mehrere Instanzen sehen dieselben Dateien, auch wo es kein geteiltes Volume gibt; damit sie sich vertragen, braucht es zusätzlich #954. Ohne Umstellung bleibt alles wie heute auf der Disk, und das bleibt ein vollwertiger Modus.

Akzeptanzkriterien:

- Je ein Schalter für Downloads, Visualisierungen und Lieferdateien samt Staging entscheidet, wo diese Art liegt: mit `Cloud` im Bucket aus `Upload:Cloud`, mit Azure Blob Storage oder S3, mit `Local` auf der Disk wie heute. Standard ist `Local`. Die Uploads wählen weiterhin mit `Upload:Backend`.
- Ein Update verlangt von keinem Hosting eine Änderung der Konfiguration: Die bestehenden Schlüssel bleiben, wie sie sind.
- Die API-Antworten und die Autorisierung ändern sich nicht.
- Ein Neustart verliert keine Lieferdatei. Für Downloads und Visualisierungen gelten dieselben Cleanup-Regeln wie auf der Disk.
- Ein Präfix mit Lieferdateien entsteht erst, wenn die Lieferung gespeichert ist. Das Cleanup löscht es nur, wenn die Datenbank die Lieferung als gelöscht führt.
- Nach Ablauf der Retention sind die Objekte eines Jobs entfernt, ohne verwaiste Präfixe.
- Startprüfung und Health Check verlangen das Verzeichnis einer Art nur, wenn sie auf der Disk liegt; `PipelineDirectory` und `ResourcesDirectory` bleiben Pflicht. Nutzt eine Art den Bucket, verlangen sie einen erreichbaren Bucket.

## Umsetzung

**Schalter.** Im bestehenden Abschnitt `Storage:` (`FileAccessOptions`) bekommt jede Art neben ihrem Verzeichnis einen Schalter mit `Local` (Standard) oder `Cloud`:

| Art | Schalter | Verzeichnis bei `Local` |
| --- | --- | --- |
| Downloads | `Storage:DownloadBackend` | `Storage:DownloadDirectory` |
| Visualisierungen | `Storage:VisualizationBackend` | `Storage:VisualizationDirectory` |
| Lieferdateien samt Staging | `Storage:AssetsBackend` | `Storage:AssetsDirectory` |
| Uploads, unverändert | `Upload:Backend` (`Cloud`, `Direct`) | `Upload:Direct:Directory` |

Den Bucket nehmen alle Arten aus `Upload:Cloud`, samt Provider und `Upload:Cloud:S3`. Steht eine Art auf `Cloud`, wird dieser Abschnitt gebunden und beim Start geprüft, auch wenn `Upload:Backend` `Direct` ist. Was nur Uploads aus dem Browser brauchen (Upload-URL, CORS, CSP-Endpunkt), gilt weiterhin nur mit `Upload:Backend` `Cloud`. Die Schlüssel unter `Upload:` ziehen nicht um, und es gibt keinen gemeinsamen Schalter: Ein Umzug der Schlüssel verlangte von jedem Hosting, sie im selben Schritt wie das Image umzustellen, und ein gemeinsamer Schalter zwänge alle Arten gleichzeitig in den Bucket. Dass der Abschnitt `Upload:Cloud` heisst, obwohl er den Bucket aller Arten beschreibt, nehmen wir vorerst in Kauf.

**Ein Bucket.** Was im Bucket liegt, liegt in dem einen Bucket aus `Upload:Cloud`, unter Präfixen nach dem heutigen Verzeichnisbaum: `uploads/` wie heute, `downloads/<jobId>/<Datei>`, `visualizations/<jobId>/<Datei>`, `assets/<jobId>/<Datei>` und das Staging unter `assets/staging/<jobId>/<Datei>`. Der Upload-Sweep löscht heute alles ausserhalb von `uploads/`; das entfällt, er räumt nur noch in `uploads/` auf. Einen Bucket, den sich zwei Installationen teilen, erkennt geopilot nicht; das README schliesst ihn aus, weil das Cleanup der einen die Präfixe der anderen für verwaist hielte. Das Verzeichnis einer Art mit `Cloud` wird nicht gelesen und ist nicht nötig. Damit entfällt der `shortcut:` in `DataDirectories.Local`: Welche Verzeichnisse geprüft werden, folgt den Schaltern.

**Provider.** `IUploadStorage` kann schon lesen, nach Präfix auflisten und löschen. Diese Operationen kommen in eine neutrale Schnittstelle für Objekte nach Schlüssel, ergänzt um Schreiben aus einem Stream und serverseitiges Kopieren. Azure und S3 implementieren sie je einmal, und `IUploadStorage` behält nur, was allein Uploads brauchen (die Upload-URL). Registriert wird der Provider, sobald eine Art den Bucket nutzt, unabhängig von `Upload:Backend`. Wie die Klassen geschnitten werden, entscheidet die Umsetzung. Bedingung ist, dass Konfiguration und Eigenheiten eines Providers an genau einer Stelle stehen.

**Stores.** `IJobFileStore` wird asynchron: lesen als Stream, schreiben aus einem Stream (statt einen beschreibbaren Stream zu liefern), prüfen, ob es eine Datei gibt, auflisten und pro Job löschen. `GetPath` entfällt, das nutzen nur Tests. Zu jeder Art kommt neben dem physischen Store einer im Bucket; ihr Schalter entscheidet, welcher registriert wird. Alles geht über die Stores, auch was heute direkt auf Verzeichnisse greift: `AssetHandler.DownloadAssetAsync` liest eine Lieferdatei heute ganz in den Speicher und streamt sie neu bis in die Antwort. Dazu kommen `AssetHandler.DeleteJobAssets` und `ProcessingJobCleanupService`. Ausgeliefert wird weiterhin durch die API, mit den heutigen Prüfungen.

**Deklaration.** Die Reihenfolge folgt #947: Die Dateien werden im Staging gehasht, dann wird die Lieferung gespeichert, und erst danach kommen die Dateien an ihren Platz. Im Bucket heisst das: jede Datei serverseitig von `assets/staging/<jobId>/` nach `assets/<jobId>/` kopieren und danach das Staging-Präfix löschen. Bricht das dazwischen ab, liegen Dateien an beiden Orten. Der nächste Cleanup-Lauf kopiert, was fehlt, und löscht dann das Staging, so wie er nach #947 ein liegengebliebenes Staging an seinen Platz schiebt. Kopieren ist wiederholbar, ein zweiter Durchgang schadet nicht. Es muss auch grosse Objekte können (Azure kopiert sie asynchron, S3 über 5 GB nur mehrteilig), denn erzeugte Dateien wie ZIP-Pakete fallen nicht unter die Grenzen der Uploads.

Eine Lieferdatei, die noch nicht unter `assets/<jobId>/` liegt, liest der Store aus dem Staging. Sonst scheiterte eine Reaktion, die gleich nach dem Speichern fällig wird, während des Kopierens, und nach einem Abbruch bis zum nächsten Cleanup-Lauf; jeder Fehlversuch kostete einen ihrer Versuche. Dasselbe gilt für den Download über den STAC-Asset-Link. Gelesen werden so nur Dateien gespeicherter Lieferungen, nach denen ein Leser fragt; ein Staging ohne Lieferung bleibt unerreichbar. Ein gemeinsames Präfix für Staging und Lieferung scheidet aus: Das Cleanup könnte dann eine nie deklarierte Lieferung nicht von einer unterscheiden, die nach dem letzten Backup deklariert wurde.

**Cleanup.** `ProcessingJobCleanupService` holt die Job-Ids über die Stores statt über Verzeichnisse und löscht pro Job über den Store, im Bucket also ein Präfix. Welche Jobs wann gehen, bestimmen weiterhin #947 und #954; diese Spec ändert nur den Weg dorthin. Beide Tickets ändern `ProcessingJobCleanupService` und `AssetHandler`. Was nach dem anderen kommt, baut auf dessen Stand auf. `Processing:DownloadRetention`, `VisualizationRetention` und `JobRetention` gelten unverändert. Lifecycle-Regeln des Providers werden nicht genutzt: Der Modus `Local` braucht dieselbe Logik ohnehin, und S3-kompatible Anbieter bieten sie nicht durchgehend an.

**Health.** Nutzt eine Art den Bucket, auch nur die Uploads, prüft der Start, ob er erreichbar und beschreibbar ist (Probe schreiben und löschen, wie heute bei den Verzeichnissen), und bricht sonst mit `Upload:Cloud` in der Meldung ab. `StorageHealthCheck` prüft dann bei jeder Abfrage, ob der Bucket erreichbar ist, dazu wie heute die Verzeichnisse der Arten mit `Local`, und bleibt beim Ready-Endpunkt. Die Antwort nennt nur den Konfigurationsschlüssel, Endpunkt und Fehler gehen ins Log, weil die Health-Endpunkte anonym antworten.

**Update und Umzug.** Ein Update ändert nichts, solange kein Schalter umgestellt wird. Jede Art zieht einzeln um, Disk und Bucket werden nicht parallel gelesen. Das README beschreibt den Umzug pro Art:

- Downloads und Visualisierungen: Schalter auf `Cloud` stellen und neu starten. Kopiert wird nichts, sie sind flüchtig, und ihre Jobs enden mit dem Stopp.
- Lieferdateien: geopilot stoppen, `Storage:AssetsDirectory` samt `staging` nach `assets/` im Bucket kopieren, `Storage:AssetsBackend` auf `Cloud` stellen, starten. Das Staging gehört dazu, weil eine gespeicherte Lieferung nach einem Absturz noch dort liegen kann; was keiner Lieferung gehört, räumt das Cleanup nach dem Start ab.
- Uploads: wie heute mit `Upload:Backend`. Ein Upload, der beim Umstellen auf einen Start wartet, muss neu hochgeladen werden.

**Doku.**

- README, Betrieb: die drei Schalter, der Bucket aus `Upload:Cloud` für alle Arten, ein Bucket pro Installation, der Umzug pro Art, dass mehr als eine Instanz #954 voraussetzt, und was gesichert werden muss: die Datenbank, der Bucket, sobald eine Art ihn nutzt, und die Verzeichnisse der Arten mit `Local`.
- CHANGELOG unter Added: Downloads, Visualisierungen und Lieferdateien können je einzeln im Bucket der Uploads liegen, mit den drei Schaltern und dem Hinweis zum Kopieren der Lieferdateien. Unter Changed: Mit Uploads im Bucket prüfen Start und Ready-Endpunkt, ob der Bucket erreichbar ist.

## Nicht im Umfang

- Ein gemeinsamer Schalter für alle Arten und der Umzug der Schlüssel unter `Upload:` nach `Storage:`.
- Das Umstellen der Hostings. Es geschieht ausserhalb dieses Repos, Art für Art.
- Das Arbeitsverzeichnis der Pipeline und die Resource-Files bleiben lokal.
- Presigned URLs für Downloads: Sie verlagern die Autorisierung auf den Besitz der URL und sind eigens zu entscheiden.
- Paralleles Lesen aus Disk und Bucket, ein Werkzeug für den Umzug und ein Umzug zwischen Providern.
- Versionierung, Replikation und Lifecycle-Regeln des Buckets sind Sache des Betriebs.
- Der Zustand der Jobs über Instanzen hinweg (#954) und die Regeln, wann das Cleanup was löscht (#947, #954). Bis #954 hält das Cleanup jeder Instanz die Jobs der anderen für verwaist, im Bucket wie auf einem geteilten Volume.

## Prüfung

- Start: Ohne die neuen Schlüssel startet geopilot wie heute, mit allen Arten auf der Disk. Eine Art mit `Cloud` ohne `Upload:Cloud` startet nicht, auch mit `Upload:Backend` `Direct`. Das Verzeichnis einer Art mit `Cloud` darf fehlen. Ein unerreichbarer Bucket stoppt den Start mit dem Schlüssel in der Meldung.
- Store-Tests, dieselben Fälle gegen Disk, Azurite und den S3-Server aus #1199 in CI: schreiben, lesen, prüfen, auflisten, pro Job löschen, kopieren.
- Deklaration im Bucket: Scheitert das Speichern, entsteht nichts unter `assets/<jobId>/`. Bricht das Kopieren nach dem Speichern ab, vervollständigt der nächste Cleanup-Lauf die Lieferung und löscht das Staging. Die Tabellentests des Cleanups aus #947 laufen auch gegen den Bucket. Eine Lieferdatei, die erst im Staging liegt, lässt sich für eine gespeicherte Lieferung lesen.
- Der Upload-Sweep lässt Objekte ausserhalb von `uploads/` stehen.
- Smoke-Test mit allen Arten auf `Cloud`, je mit Azurite und dem S3-Server: hochladen im Wizard und über die maschinelle Lieferung, eine Pipeline erzeugt einen Download, eine Visualisierung und eine Lieferung, die per Ablage-Reaktion weitergeht. Nach einem Neustart ist die Lieferdatei über den STAC-Asset-Link abrufbar. Dazu eine Mischform: Uploads mit `Direct`, Lieferdateien mit `Cloud`, Downloads und Visualisierungen mit `Local`, erreicht über den Umzug nach README (Lieferungen auf der Disk kopieren, umstellen, starten, eine alte Lieferung abrufen). Ohne die neuen Schlüssel laufen dieselben Abläufe wie heute.
- CI ist grün, auch der Abgleich der generierten Typen: Der API-Vertrag ändert sich nicht.
