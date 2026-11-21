# Job- und Upload-Zustand als DB-Read-Model persistieren (Mehrinstanzbetrieb) (#954)

Review: required. Das Protokoll wird zum Lesepfad: ein neues architektonisches Konzept, das ADR 0010 ablöst. Dazu ändern sich anonym erreichbare Endpunkte und der Umgang mit Uploads, und die Betreiberdoku nimmt Aussagen zurück. Die Migrationen fügen nur Tabellen und Spalten hinzu.

## Ziel

Zwei Instanzen der API vertragen sich, wenigstens während eines Rollouts. Heute liegen Upload-Sessions und Jobs nur im Speicher der Instanz, die sie angelegt hat. Eine zweite Instanz beantwortet die Statusabfrage mit 404 und kennt einen dort initiierten Upload nicht, und beide Cleanup-Dienste löschen, was ihr eigener Speicher nicht kennt, auch nach jedem Neustart.

Persistiert wird ein Lesemodell, nicht die Ausführung. Ein Job läuft auf der Instanz, die ihn angenommen hat, Preflight, Warteschlange und Runner bleiben im Prozess. Gelesen wird aus dem Ausführungsprotokoll selbst, nicht aus einer zweiten Tabelle.

Akzeptanzkriterien:

- Jede Instanz beantwortet für jeden Job, ob im Browser oder maschinell gestartet, Statusabfrage, Downloads, Visualisierungen und Deklaration, auch wenn er auf einer anderen Instanz läuft oder lief; einen maschinellen Versuch auch unter `api/v1/submission`.
- Ein Upload lässt sich auf jeder Instanz starten, wo immer er initiiert wurde, und pro Upload läuft über alle Instanzen höchstens ein Job.
- Kein Cleanup löscht etwas, weil der Speicher der eigenen Instanz es nicht kennt. Uploads überstehen einen Neustart.
- Ein Lauf, der vor einem Neustart endete, bleibt danach abfragbar.
- ADR 0020 löst ADR 0010 ab.

## Umsetzung

Zwei PRs, beide wie bisher mit einer Instanz ausgerollt; erst danach dürfen es zwei sein. Der erste steht für sich, der zweite braucht ihn für die Sperre pro Upload.

### PR 1: Upload-Sessions in der Datenbank

Der `UploadStore` im Speicher wird durch eine Tabelle ersetzt: eine Zeile pro Session (Upload-Id, Zeitpunkt der Initiierung) und ihre angemeldeten Dateien als Kindtabelle (Name, Storage-Key, angemeldete Grösse, Content-Type), gebaut wie `RecordingFiles`. Die Initiierung (`POST api/v2/upload` und die Multipart-Route der maschinellen Lieferung) schreibt die Session, die Freigabe des Uploads und der Sweep löschen sie. Alle heutigen Leser lesen dort: Jobstart, Preflight, `RegisterJobFiles`, die Mandatsübersicht mit ihrem Filter nach Dateiendungen, der Direct-Upload-Endpunkt und die Prüfung von `Upload:MaxActiveJobs`.

`MaxActiveJobs` gilt damit für alle Instanzen zusammen statt pro Prozess. Zwei Initiierungen an der Grenze können sie um eins überschreiten; das steht als `shortcut:` im Code.

Der Upload-Sweep fragt die Tabelle statt des Speichers. Ein Präfix ohne Session bleibt verwaist und geht, eine Session ohne Dateien verfällt wie heute nach zweimal `Upload:UploadUrlExpiryMinutes`. Weil die Sessions einen Neustart überstehen, löscht der Sweep danach nicht mehr alle Uploads.

### PR 2: Das Protokoll als Lesepfad

**Neue Spalten an `Recordings`**, alle nullable:

- `QueuedAt`: wann der Preflight bestanden war und der Job auf einen Platz zu warten begann. Ohne sie unterscheidet das Protokoll «Preflight läuft» nicht von «wartet», denn ohne ClamAV bleibt der Scan-Status `NotScanned`.
- `HeartbeatAt`: siehe Lebendigkeit.
- `PipelineDisplayName` (`LocalizedText` als `jsonb`): der Name der Pipeline in der Statusantwort. Heute kommt er aus der lebenden Pipeline, im Snapshot steckt er nur im JSON.
- `SubmissionMandateKey`: der Schlüssel, mit dem ein maschineller Versuch sein Mandat adressierte, gesetzt genau dann, wenn der Start eine Lieferabsicht trug. Daran erkennt der Status-Endpunkt der maschinellen Lieferung einen Versuch, und er gibt den Wert zurück, so, wie er beim Start lautete. Das Mandat allein genügt dafür nicht: Es steht an jedem Lauf, auch an einem aus dem Browser, und einen Versuch kann auch eine Person mit Benutzer-Token starten, also sagt es auch `MachineClientId` nicht. Den übrigen Ausgang eines Versuchs, den das Issue als zwei neue Felder vorsah, liefern seit #1141 und #1189 schon `Deliveries.JobId` und `DeclarationFailure`. Die Lieferangaben (Vorgänger, Teillieferung, Kommentar, Metadaten) bleiben im Speicher am Job: Nur die Instanz, die am Ende deklariert, braucht sie, und ein Freitext gehört nicht in einen Datensatz mit zehn Jahren Aufbewahrung.

Dazu ein partieller Index auf `UploadId` für Läufe ohne Terminal-Status, für die Sperre pro Upload.

**Lebendigkeit.** Ein Lauf ist abgeschlossen, sobald er einen Terminal-Status hat und, als maschineller Versuch mit lieferbarem Ausgang, dazu eine Lieferung oder `DeclarationFailure`. Bis dahin frischt die Instanz, die den Job hält, `HeartbeatAt` auf: Der Startsatz setzt ihn, danach aktualisiert ein Hintergrunddienst pro Instanz alle 30 Sekunden in einer einzigen Anweisung die Läufe aller Jobs, deren Folgen sie noch nicht getragen hat, also bis `JobLifecycle.RunFinishedAsync` oder der gescheiterte Preflight durch ist. Beim Herunterfahren stoppt dieser Dienst erst nach dem Runner. Sonst gälte ein Lauf, der im Drain-Fenster (`Processing:ShutdownDrainTimeout`) noch rechnet, nach drei Minuten als tot, und derselbe Upload liesse sich anderswo starten. Ein Lauf, der nicht abgeschlossen ist, lebt, solange sein Heartbeat jünger als drei Minuten ist; sonst ist er tot, sein Ausgang unbekannt. Die drei Minuten sind die Frist zwischen zwei Heartbeats, nicht die erlaubte Dauer eines Schritts: Der Dienst hängt nicht an der Pipeline, und ein Schritt, der eine halbe Stunde rechnet, im Prozess oder im ilitools-wrapper, wird in dieser Zeit weiter aufgefrischt. Ohne die Bedingung für Versuche stünde ein Versuch, dessen Instanz zwischen Terminal-Status und Deklaration starb, bis zum Ende von `JobRetention` auf `Processing`. Ein Lauf ohne Heartbeat, also von vor der Migration, ist tot, wenn er nicht abgeschlossen ist.

Geschrieben und verglichen wird mit der Uhr der Datenbank, wie im `ReactionStore`, damit Instanzen mit abweichender Uhr sich nicht gegenseitig für tot erklären. Die Regel steht an einer Stelle, als Ausdruck auf dem Lauf, und Statusabfrage, Sperre pro Upload und Cleanup verwenden sie; #891 baut später darauf auf. Intervall und Schwelle sind Konstanten mit `shortcut:`, die Schwelle ein Mehrfaches des Intervalls, damit ein verpasster Schreibvorgang keinen Lauf für tot erklärt.

Das weicht vom Issue ab, das einen Lauf ohne Terminal-Status nach `Processing:JobTimeout` für tot hielt: Seit die Warteschlange gedrosselt ist (#887), kann ein gesunder Job länger warten, und das Cleanup löschte seine Dateien.

Konnte eine Instanz ihren Heartbeat zweieinhalb Minuten lang nicht schreiben (gemessen mit ihrer eigenen Uhr seit dem letzten Erfolg), bricht sie alle Jobs ab, die sie hält (`Cancelled`, Upload bleibt), bevor die anderen sie für tot halten. Sonst arbeitete ein abgeschnittener Lauf weiter, während derselbe Upload anderswo neu startet, und gäbe an seinem Ende den Upload unter dem neuen Job frei. Aus demselben Grund frischt der Heartbeat nur Läufe auf, die nach der Regel oben noch leben (eine Bedingung in derselben Anweisung), und einen Job, dessen Lauf er nicht mehr auffrischen konnte, bricht die Instanz ebenso ab. So belebt eine Instanz, die länger als die Schwelle stillstand, etwa in einem eingefrorenen Container, beim Aufwachen keinen Lauf wieder, den die anderen schon für tot halten. «Tot» heisst damit, dass niemand mehr an dem Lauf arbeitet. Der Preis: Ein Datenbankausfall von mehr als zweieinhalb Minuten bricht alle laufenden Jobs ab.

**Harte Schreibvorgänge.** Was die Lesepfade lesen, schreibt der Recorder mit Wiederholung: `QueuedAt` zusammen mit dem Scan-Ergebnis, Start und Abschluss eines Schritts (Zustand, Meldungen, Artefakte), das Scheitern des Preflights, den Terminal-Status und `DeclarationFailure`. Die Wiederholung sitzt im Recorder und nicht bei den Aufrufern, weil die Pipeline ihre Callbacks inline abwartet: wenige Versuche mit kurzer Pause, nur bei transienten Fehlern, jeder mit einem frischen `Context` aus der registrierten `IDbContextFactory`, sonst arbeitet er auf einem verdorbenen Change Tracker. `EnableRetryOnFailure` scheidet aus, weil es eigene Transaktionen verbietet und die Testbasis pro Test eine Transaktion zurückrollt.

Scheitert ein Schreibvorgang endgültig, endet der Job als `Cancelled` und behält seinen Upload, wie nach einem Timeout: Die Datenbank hat den Lauf unterbrochen, an den Daten liegt es nicht, und derselbe Upload lässt sich neu starten. Das weicht vom Issue ab, das hier `Failed` vorsah; `Failed` gäbe den Upload sofort frei (`JobLifecycle.Decide`), ein längerer Datenbankausfall löschte so die Uploads aller laufenden Jobs. Im Preflight braucht es dafür einen Weg neben dem gescheiterten Preflight, der den Upload immer freigibt. In einem Schritt wirft der Callback, und der Runner meldet den Lauf mit einem eigenen `RunOutcome`, das `Decide` wie einen Timeout behandelt. Terminal-Status, Scheitern des Preflights und `DeclarationFailure` folgen auf einen schon beendeten Lauf; nach dem letzten Versuch wird nur protokolliert, und der Lauf gilt danach als tot. Weich bleiben die Eingaben pro Schritt und die Ereignisse der Reaktionen.

**Reihenfolge am Laufende.** `JobLifecycle.RunFinishedAsync` schreibt den Terminal-Status, bevor es die maschinelle Lieferung deklariert, denn die Deklaration liest ihn jetzt aus der Datenbank. Terminal-Status und Lieferartefakte entstehen weiterhin im selben `SaveChanges`: Es gibt keinen lieferbaren Lauf mit unvollständigem Liefermanifest.

**Lesepfade.** Ein einziger Pfad, auch auf der Instanz, die den Job ausführt:

- `GET api/v2/processing/{jobId}`: Zustand aus dem Terminal-Status, sonst `Running` mit `QueuedAt`, sonst `Pending`. Der Preflight-Schritt steht wie heute auf `Error`, wenn der Lauf scheiterte, bevor ein Schritt begann. Schritte, Meldungen, Downloads, Lieferdateien und Visualisierungen kommen aus den Schritt- und Artefaktzeilen, `WithRunningStepWhileJobRuns` bleibt. Die Antwort ändert sich weder in Form noch in Inhalt.
- `GET api/v1/submission/{id}` und seine Downloads: Ein Versuch ist ein Lauf mit `SubmissionMandateKey`. Lesen darf ihn, wessen `UserId` oder `MachineClientId` er trägt. Der Zustand folgt aus Terminal-Status, `DeclarationFailure` und `Deliveries`.
- Den Originalnamen eines Downloads lesen beide Controller aus den Artefaktzeilen.
- Die Deklaration (`DeliveryDeclarationService`, im Web und maschinell): lieferbar nach dem Terminal-Status, deklarierbar nach `Declarability`, das Mandat aus dem Lauf. `AssetHandler.RecordJobAssetsAsync` nimmt die Lieferdateien aus den Artefakten der Art `Delivery` (`OriginalFileName`, `PersistedFileName`, `FromUpload`); dafür reicht das Schema.

Alle antworten nur innerhalb des heutigen Fensters: solange der Lauf lebt, und wenn er abgeschlossen ist, bis `Processing:JobRetention` seit `TerminalAt` abgelaufen ist. Danach und für einen toten Lauf antworten sie 404 wie heute. So bleibt die anonym erreichbare Statusabfrage auf dem heutigen Stand, obwohl das Protokoll zehn Jahre bleibt, und die Deklaration braucht das Fenster ohnehin, weil die bereitgestellten Lieferdateien mit dem Job gehen.

**Ein aktiver Job pro Upload.** Der Start sperrt die Zeile der Upload-Session (`SELECT ... FOR UPDATE`) in derselben Transaktion wie den harten Startsatz und lehnt mit `JobAlreadyActiveException` ab (409 wie heute), wenn für den Upload ein lebender Lauf existiert. Ein toter Lauf hält seinen Upload nicht fest. Die Prüfung im Speicher in `ProcessingJobStore.CreateJob` fällt weg.

**Cleanup.** `ProcessingJobCleanupService` entscheidet nach dem Lauf in der Datenbank statt nach dem Speicher. Als Ende eines abgeschlossenen Laufs gilt `TerminalAt`, eines toten `HeartbeatAt`, ohne Heartbeat `StartedAt`. Download- und Visualisierungsverzeichnisse gehen nach ihrer Retention ab diesem Ende, die übrigen Verzeichnisse eines Jobs nach `JobRetention` wie heute. Ein lebender Lauf behält alles, ein hängender wird wie heute nur gemeldet. Ein Verzeichnis ohne Lauf ist verwaist. Einzige Ausnahme ist das Arbeitsverzeichnis der Pipeline, das lokal auf der Instanz liegt: Es entsteht schon, wenn die Pipeline gebaut wird, also bevor der Startsatz committet ist, deshalb zählt dafür auch der Speicher der eigenen Instanz. Über Liefer- und Staging-Verzeichnisse entscheiden nur die Lieferungen des Jobs, nach der Regel aus #947: Ein Lieferverzeichnis ohne Lieferung bleibt und wird gemeldet, denn nach dem Restore einer älteren Datenbank fehlen ihr spätere Lieferungen samt ihren Läufen. Jobs im eigenen Speicher nimmt jede Instanz weiterhin selbst heraus.

**Was im Speicher bleibt.** `ProcessingJobStore` hält, was zur Ausführung gehört: die lebende Pipeline, die Warteschlange, die Lieferabsicht mit den Lieferangaben. Seine Bemerkung zur Annahme genau einer Instanz wird ersetzt.

**Doku.**

- ADR 0020 in `geopilot-doc` löst 0010 ab: Persistiert ist das Lesemodell, Ausführung und Eigentum am Job bleiben bei der annehmenden Instanz, ohne Claiming, Queue-Tabelle oder Wiederaufnahme. Ob ein Lauf lebt, sagt der Heartbeat, und eine Instanz ohne Heartbeat bricht ihre Jobs selbst ab. Verworfen sind eine zweite, veränderliche Zustandstabelle (ein dritter Schreibvorgang mit demselben Inhalt an denselben Stellen), ein hybrider Lesepfad (die ausführende Instanz liest aus dem Speicher, die anderen aus der Datenbank; zwei Pfade, deren Abweichung erst im Cluster auffällt) und Option 2 aus 0010 in vollem Umfang.
- `docs/Ausfuehrungsprotokoll.md`: «Das Protokoll wird nur geschrieben» gilt nicht mehr. Das Protokoll ist jetzt auch der Lesepfad für Status und Deklaration, macht einen Lauf aber weiterhin nicht wieder ausführbar. Lebendigkeit und neue Spalten kommen dazu.
- `docs/MaschinelleAnlieferung.md`, «Lebensdauer»: Ein Versuch steht jetzt in der Datenbank und bleibt über einen Neustart abfragbar, wenn sein Lauf vorher endete.
- README, Betrieb: was mehr als eine Instanz voraussetzt (unten), dass `Upload:MaxActiveJobs` für alle Instanzen zusammen gilt und dass ein Datenbankausfall von mehr als zweieinhalb Minuten laufende Jobs abbricht.
- CHANGELOG unter Changed: Uploads überstehen einen Neustart der Applikation, ebenso der Status eines Laufs, der vorher endete, auch eines maschinellen Versuchs. `Upload:MaxActiveJobs` gilt für alle Instanzen zusammen.

## Nicht im Umfang

- Für einen toten Lauf bleibt es bei 404. Einen Vertrag für den verlorenen Job bringt #891, er baut auf der Lebendigkeit auf.
- Keine Wiederaufnahme, kein Neustart eines Laufs, keine verteilte Warteschlange: Ein Job, dessen Instanz stirbt, bleibt verloren.
- Die gemeinsame Ablage der Artefakte (#850). Für mehr als eine Instanz müssen Download-, Visualisierungs- und Asset-Verzeichnis samt Staging für alle Instanzen dieselben sein, über einen gemeinsamen Mount oder #850, im Direct-Modus auch das Upload-Verzeichnis. Das Arbeitsverzeichnis der Pipeline bleibt lokal.
- Prozesslokal bleiben der Rate-Limiter der Initiierung und `Processing:MaxConcurrentJobs`, das bewusst pro Instanz gilt.
- Die Multipart-Route legt ihre Session weiterhin erst nach den Dateien an (#1088).
- Herunterfahren und Drain (#1024).

## Prüfung

- Tests für jeden Lesepfad mit leerem Job-Speicher und dem Lauf nur in der Datenbank, wie ihn eine fremde Instanz sieht: Statusabfrage (wartend, laufend, Preflight gescheitert, beendet, tot, Fenster abgelaufen), Status und Download eines Versuchs, Deklaration im Web und maschinell.
- Lebendigkeit als Tabellentest: Terminal-Status, frischer, abgelaufener und fehlender Heartbeat, ein Versuch mit lieferbarem Ausgang ohne Lieferung und ohne `DeclarationFailure`.
- Zwei gleichzeitige Starts desselben Uploads über zwei Verbindungen: einer bekommt 409. Ein toter Lauf blockiert einen Start nicht. Lässt die Testbasis mit ihrer Transaktion pro Test keine zwei Verbindungen zu, deckt der Smoke-Test den Fall.
- Recorder: Der erste Versuch scheitert, der zweite mit frischem `Context` gelingt; scheitern alle, endet der Job als `Cancelled` und sein Upload bleibt, im Preflight wie in einem Schritt.
- Heartbeat: Ein Schritt, der länger als die Schwelle läuft, hält seinen Lauf am Leben. Eine Instanz, die ihren Heartbeat nicht schreiben kann, bricht ihre Jobs ab, bevor die anderen sie für tot halten. Einen Lauf, den die Datenbank schon für tot hält, frischt der Heartbeat nicht wieder auf, und sein Job bricht ab. Ein Lauf im Drain-Fenster bleibt auch dann am Leben, wenn das Fenster länger ist als die Schwelle.
- Sweep und Cleanup: Ein Upload und ein Job, die nur in der Datenbank stehen, bleiben. Die Verzeichnisse eines toten Laufs gehen nach `JobRetention` ab seinem letzten Heartbeat.
- Smoke-Test mit zwei Instanzen gegen eine Datenbank, mit Cloud-Backend, derselben Pipeline-Definition und gemeinsamen Verzeichnissen: im Wizard auf A hochladen, auf B starten, auf A abfragen, auf B liefern; dasselbe als maschinelle Lieferung. Dann A während eines Laufs hart beenden: Sein Status wird nach drei Minuten 404, der Upload bleibt, und derselbe Upload startet auf B neu. Ein Neustart nach einem beendeten Lauf lässt dessen Status stehen.
- `has-pending-model-changes` meldet nichts, CI ist grün, auch der Abgleich der generierten Typen: Der API-Vertrag ändert sich nicht.
