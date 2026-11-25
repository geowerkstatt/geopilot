# Ausführungsprotokoll

geopilot hält zu jeder [Verarbeitung](../GLOSSARY.md#verarbeitung) ein dauerhaftes Ausführungsprotokoll in der Datenbank fest: was lief, auf welcher Fassung der [Pipeline-Definition](../GLOSSARY.md#pipeline), für wen, und wie es endete. Das [Protokoll](../GLOSSARY.md#protokoll) ist der Prüfnachweis des Lieferprozesses. Es überlebt Neustarts und das [Retirement](../GLOSSARY.md#retirement), und zwar ausdrücklich auch für abgelehnte und abgebrochene Verarbeitungen, die sonst keine Spur hinterlassen.

Das Protokoll ist zugleich der Lesepfad einer Verarbeitung. Ihren Status samt Downloads und Visualisierungen und was die Abgabe ihrer [Lieferung](../GLOSSARY.md#lieferung) braucht, liest jede Instanz aus dem Protokoll, auch für eine Verarbeitung, die auf einer anderen Instanz läuft oder vor einem Neustart endete (siehe [Lebendigkeit](#lebendigkeit)). Wieder ausführbar macht es eine Verarbeitung nicht: Es beschreibt, was lief, und ausgeführt wird eine Verarbeitung nur von der Instanz, die sie angenommen hat.

## Was aufgezeichnet wird

Pro [Verarbeitung](../GLOSSARY.md#verarbeitung) entsteht beim Start ein [Protokoll](../GLOSSARY.md#protokoll) (Tabelle `Recordings`) mit:

- der gelaufenen [Pipeline](../GLOSSARY.md#pipeline) mit ihrem Anzeigenamen, wie er beim Start lautete (`PipelineDisplayName`), und einem **Definitions-Snapshot** (siehe unten),
- den **Bindungen**, also dem, was geopilot beim Start aus der Datenbank an die Verarbeitung bindet (siehe unten),
- Mandat und Urheber, entweder der Benutzer oder der Maschinen-Client (`UserId` oder `MachineClientId`, nie beide zugleich, was die Datenbank erzwingt; beide leer, wenn die Verarbeitung anonym auf einem öffentlichen Mandat gestartet wurde; anonym darf prozessiert, aber nicht geliefert werden, und wer geliefert hat, steht an der [Lieferung](../GLOSSARY.md#lieferung) selbst) sowie der Art des Clients (`WebClient`, `ApiClient`, `Unknown`), klassifiziert aus dem Request und nie als roher Header gespeichert. Die Art des Clients sagt, auf welchem Weg die Verarbeitung gestartet wurde, nicht von wem: eine Person, die die API direkt aufruft, etwa aus Swagger, ist ebenso `ApiClient` wie ein Maschinen-Client, und wer es war, steht in den beiden Id-Spalten,
- bei einer maschinellen Lieferung der Kennung, die der Client für die Lieferung mitgeschickt hat (`ClientReference`), so wie sie kam, ohne Prüfung gegen frühere [Versuche](../GLOSSARY.md#versuch). Versuche mit derselben Kennung, demselben Urheber und demselben Mandat sind Versuche derselben Lieferung,
- bei einem [Versuch](../GLOSSARY.md#versuch) dem Schlüssel seines Mandats, wie er beim Start lautete (`SubmissionMandateKey`). Gesetzt ist er genau dann, wenn der Start eine [Lieferabsicht](../GLOSSARY.md#lieferabsicht) trug. Daran erkennt geopilot einen Versuch, nicht am Maschinen-Client, denn auch eine Person kann einen starten,
- der **Lieferberechtigung** (`PermissionToDeliver`), also ob der Urheber auf dem Mandat abgeben darf, unabhängig davon, ob die Daten eine Lieferung zulassen: `Permitted`, wenn der Urheber Mitglied einer Organisation des Mandats ist und das Mandat Lieferungen annimmt, sonst `NotPermitted`, auch bei einem anonymen Start. Entschieden wird einmal beim Start. Protokolle, die entstanden, bevor geopilot diese Angabe festhielt, haben keine (`NULL`): unbekannt, nicht «nicht berechtigt»,
- dem Upload-Manifest (`RecordingFiles`): Dateiname, Storage-Key, deklarierte Grösse, und nach dem Virenscan der SHA-256 jeder Datei,
- dem Resultat des Virenscans (`Clean`, `ThreatDetected` mit Details, oder `NotScanned` wenn die Prüfung deaktiviert ist),
- dem Zeitpunkt, zu dem der Preflight bestanden war und die Verarbeitung auf einen Platz in der Warteschlange zu warten begann (`QueuedAt`),
- dem letzten Heartbeat der Instanz, die die Verarbeitung ausführt (`HeartbeatAt`, siehe [Lebendigkeit](#lebendigkeit)),
- der Applikationsversion und allen Zeitstempeln in UTC.

Pro Schritt (`RecordingSteps`) werden festgehalten: Status, Beginn und Ende, die Prozessor-Implementierung samt Assembly und Version, Fehlermeldung bei Abbruch, Statusmeldung, sowie die **Auswertung jeder geprüften Condition** (`RecordingConditions`): Ausdruck, optionale stabile Kennung (`id` in der Definition), die referenzierten Werte und ob sie zutraf. Auch nicht zutreffende Auswertungen werden festgehalten, damit "geprüft und nicht zugetroffen" von "nie geprüft" unterscheidbar bleibt. Die Namen der erzeugten Artefakte (Downloads, Visualisierungen, Lieferdateien) stehen in `RecordingArtifacts`.

Wirft ein Prozessor eine Exception, steht sie als Fehlermeldung an seinem Schritt. Am Protokoll selbst steht ein Grund (`FailureReason`) nur, wo kein Schritt der Pipeline ihn nennt: wenn der Preflight scheiterte, der keine Schritt-Zeile hat (siehe [Wo ist der Preflight?](#wo-ist-der-preflight)), die Verarbeitung in den Timeout lief, die Instanz herunterfuhr oder die Verarbeitung abbrach (siehe [Lebendigkeit](#lebendigkeit)), das Protokoll nicht mehr geschrieben werden konnte, oder das Ablegen der Downloads oder das Bereitstellen der Lieferdateien scheiterte.

Als Version eines Schritts (`ProcessAssemblyVersion`) hält das Protokoll, wie bei der Applikationsversion, die `InformationalVersion` der Assembly fest, aus der die Prozessor-Implementierung stammt; fehlt sie, die Assembly-Version. Bei einem Plugin nennt sie so den Build, der lief, samt Vorabzusatz: `1.4.0-preview.12`, wo die Assembly-Version nur `1.4.0.0` hergibt. Hält der Build seinen Commit fest, folgt dieser nach einem `+`. Die eingebauten Prozessoren stammen aus `Geopilot.Pipeline`, das eine feste Version hat und mit geopilot gebaut wird. Ihre Version nennt denselben Commit wie die Applikationsversion und sagt nichts, was diese nicht schon sagt.

Die **Eingaben** jedes Schritts stehen in `RecordingInputs`: was sein Prozessor beim Start bekam, eine Zeile pro Wert oder Datei, in der Reihenfolge seiner Parameter und auch innerhalb von Listen pro Element. Jede Zeile nennt den Parameter (`Parameter`), die Herkunft (`Origin`) und die Art (`Kind`: `Value`, `File` oder `Absent`):

- `Definition`: ein Wert, der in der Pipeline-Definition steht.
- `StepOutput`: ein Ergebnis eines früheren Schritts, in `Reference` als `schritt.Eigenschaft`. Ein Bezug auf einen übersprungenen Schritt steht als `Absent` da: Er trägt nichts bei, so wie bei der Bindung der Eingaben.
- `Resource`: eine Datei aus `${file(...)}`, in `Reference` ihr Pfad unter dem Ressourcenverzeichnis.
- `Upload`: eine Datei aus `${upload()}`.
- `Configuration`: eine Datei, die die Konfiguration des Prozessors über einen Parameter vom Typ `IPipelineFile` übergibt, etwa `refMappingFile`, in `Reference` ihr Pfad unter dem Ressourcenverzeichnis.

Ein Wert steht als Text in `Value` (Invariant Culture, nach 500 Zeichen abgeschnitten), eine Datei mit Name und Grösse (`FileName`, `Size`). Ressourcen und Dateien aus der Konfiguration tragen zusätzlich den SHA-256, berechnet beim Beginn des Schritts; so bleibt nachvollziehbar, welchen Stand einer Ressource ein Schritt bekam. Ein Upload trägt Grösse und SHA-256 aus dem Manifest, den SHA-256 nur, soweit der Virenscan ihn berechnet hat. Reicht ein früherer Schritt einen Upload unverändert weiter, etwa ein Matcher, markiert ihn `FromUpload`; seine Grösse kommt dann ebenfalls aus dem Manifest, einen Hash trägt er dort nicht, den trägt die Upload-Zeile des Schritts, der ihn aus dem Upload bekam. Andere Dateien aus früheren Schritten stehen ohne Hash da. Ein Schritt, dessen Prozessor nicht lief (übersprungen, per Pre-Condition gescheitert oder mit Eingaben, die sich nicht binden liessen), hat keine Eingaben; den Grund nennen seine Conditions oder seine Fehlermeldung.

Das Protokoll setzt dabei an der Grenze des Schritts an. Was ein Prozessor intern tut oder ein entfernter Dienst auflöst, etwa Modelle und Referenzdaten über `modelDirs`, hält es nicht fest; dafür bleibt das Log des Werkzeugs.

Scheitert die Lieferung einer Verarbeitung, deren Daten sie zuliessen und deren Urheber abgeben durfte, steht der Grund in `DeliveryFailure`, bei einer Abgabe im Web wie bei einer [maschinellen Lieferung](MaschinelleLieferung.md); bei dieser meldet der Versuch denselben Text in `messages`. Er besteht aus festen Meldungen, nie aus dem Inhalt der Lieferangaben, denn ein Kommentar ist Freitext und gehört nicht in einen Datensatz, der jahrelang aufbewahrt wird. Scheitern mehrere Versuche, steht der Grund des letzten da; gelingt ein späterer, bleibt er stehen. Leer heisst: auf Anhieb geliefert, nie versucht oder nur abgewiesen, weil schon eine Lieferung bestand; bei einem älteren Protokoll ohne `PermissionToDeliver` ist es unbekannt. Ob eine Lieferung entstand, sagt die Lieferung selbst (`Deliveries.JobId`).

Wird die Lieferung einer Verarbeitung weitergegeben (siehe [Reaktionen nach dem Abschluss](pipeline/Pipelines.md#reaktionen-nach-dem-abschluss)), steht jeder [Zustellversuch](../GLOSSARY.md#zustellversuch) ihrer [Reaktionen](../GLOSSARY.md#reaktion) in `RecordingReactionEvents`: die Lieferung, die Id der Reaktion im [Reaktions-Satz](../GLOSSARY.md#reaktions-satz), ob die Reaktion die Lieferung weitergibt oder begleitet, der [Kanal](../GLOSSARY.md#kanal), die Nummer des Versuchs, Beginn und Ende, der Zustand der Reaktion danach, die Meldung des Kanals, die geopilot-Version der Instanz, die den Zustellversuch ausführte (`AppVersion`), und die Konfiguration, mit der sein Kanal lief (`Config`). Die Meldung steht in `Message` pro Sprache (`de`, `fr`, `it`, `en`) als JSON-Objekt; was ein Server oder eine Ausnahme dazu meldet, steht in allen Sprachen unübersetzt darin. Personendaten des Lieferanten stehen nur darin, wenn ein Mailserver seine Adresse abweist (siehe [Aufbewahrung](#aufbewahrung)); die Meldung kann den Zielpfad enthalten. Wird die Lieferung gelöscht, kommt pro Reaktion eine Zeile `Deletion` dazu, mit dem Benutzer, der gelöscht hat, und dem Zustand der Reaktion danach: `Done` hatte die Lieferung schon weitergegeben, `Error` war schon endgültig gescheitert, `Deleted` endete mit der Löschung. Setzt ein Admin Reaktionen zurück, die aufgegeben hatten, kommt pro Reaktion eine Zeile `Reset` dazu, mit dem Admin und dem Zustand `Pending`; die Versuche danach zählen wieder ab 1, die Zeilen davor behalten ihren Ausgang. Die Spalte `UserId` nennt nur, wer ein Ereignis ausgelöst hat: Einen Zustellversuch führt die Applikation aus, dort bleibt sie leer. Wer geliefert hat, steht an der Lieferung (`DeliveredByUserId` oder `DeliveredByClientId`), wer die Verarbeitung gestartet hat, am Protokoll.

`Config` hält pro Parameter des Kanals, der einen Wert hat, den Wert fest, wie er konfiguriert ist (`value`), und woher er kommt (`origin`): `base` aus der Basis-Konfiguration der Installation (`Pipeline:ChannelConfigs`), `definition` aus der Pipeline-Definition (die `default_config` des Kanals mit den `channel_config_overwrites` der Reaktion, beim Erstellen der Lieferung eingefroren) oder `mandate` aus dem Mandat. Bei einer Datei aus dem Ressourcen-Verzeichnis, etwa dem Template einer Mail, ist `value` ihr Pfad, und `sha256` hält den Hash ihres Inhalts beim Zustellversuch fest; fehlt die Datei oder liegt sie ausserhalb des Ressourcen-Verzeichnisses, gibt es keinen Hash. Jeder Zustellversuch liest Basis-Konfiguration und Ressourcen neu, zwei Zustellversuche derselben Reaktion können also verschieden laufen, und zwischen zwei Zustellversuchen kann ein Update liegen. Geheime Werte, etwa das Passwort des Mail-Kanals, stehen nur als `**********` darin; der Schlüssel bleibt, damit sichtbar ist, dass ein Wert gesetzt war. Für den Mail-Kanal etwa:

```json
{
  "host": { "value": "smtp.example.com", "origin": "base" },
  "password": { "value": "**********", "origin": "base" },
  "template": { "value": "mail/info.html", "origin": "definition", "sha256": "9f86d081..." },
  "to": { "value": ["empfang@example.com"], "origin": "mandate" }
}
```

Zeilen `Reset` und `Deletion` haben weder `AppVersion` noch `Config`, ebenso Zustellversuche, die geopilot vor dieser Erweiterung festhielt. Ist der Kanal einer Reaktion unbekannt, fehlt nur `Config`; warum der Zustellversuch scheiterte, steht in der Meldung.

## Schreibzeitpunkte und Verlässlichkeit

- **Beim Start der [Verarbeitung](../GLOSSARY.md#verarbeitung), hart:** Kann der Startdatensatz nicht geschrieben werden, wird die Verarbeitung nicht angenommen und der Aufrufer erhält einen Fehler statt einer Bestätigung. Eine angenommene Verarbeitung ohne [Protokoll](../GLOSSARY.md#protokoll) gibt es nicht.
- **Was die Lesepfade brauchen, mit Wiederholung:** Den bestandenen Preflight samt Scan-Ergebnis und `QueuedAt` sowie Beginn und Abschluss jedes Schritts (Zustand, Meldungen, Artefakte) schreibt geopilot nach einem vorübergehenden Datenbankfehler noch zweimal, nach einer und nach zwei Sekunden Pause. Bleibt die Datenbank auch dann unerreichbar, wird die Verarbeitung abgebrochen: Sie endet als `Cancelled` und behält ihren Upload wie nach einem Timeout, denn an den Daten liegt es nicht. Ebenso wiederholt werden das Scheitern des Preflights, der Terminal-Status und `DeliveryFailure`. Sie halten ein Ende fest, das schon feststeht, deshalb wird ein endgültiger Fehler dort nur geloggt.
- **Sonst weich:** Die Eingaben der Schritte und die Ereignisse der [Reaktionen](../GLOSSARY.md#reaktion) brechen bei einem Schreibfehler nichts ab, er wird als Warnung geloggt.
- Alle Schritt-Zeilen werden mit dem Startdatensatz als `Pending` angelegt, beim Erreichen auf `Running` gesetzt und beim Abschluss aktualisiert. Das Protokoll einer unterbrochenen Verarbeitung zeigt so das volle Bild: erledigte Schritte tragen ihren Endzustand, der laufende steht auf `Running`, und ein Schritt, der `Pending` geblieben ist, wurde nie erreicht.
- Die Eingaben eines Schritts werden geschrieben, sobald er sie gebunden hat und bevor sein Prozessor läuft. Das Protokoll einer unterbrochenen Verarbeitung zeigt so auch, womit der laufende Schritt arbeitete.
- **Ein Protokoll ohne Terminal-Status, dessen Verarbeitung tot ist, bedeutet "[Ausgang](../GLOSSARY.md#ausgang) unbekannt":** die Instanz ist während des Laufs gestorben (Restart-Opfer), oder sie konnte den Terminal-Status nicht mehr schreiben. Mit frischem Heartbeat läuft die Verarbeitung dagegen noch (siehe [Lebendigkeit](#lebendigkeit)). Ein sauberer Stopp wird als `Cancelled` mit Grund `host shutdown` festgehalten, ein harter Abbruch hinterlässt keinen Terminal-Status. Beides ist zählbar und macht messbar, wie oft Neustarts laufende Verarbeitungen treffen (Re-Evaluations-Trigger von ADR 0020).
- Ein [Zustellversuch](../GLOSSARY.md#zustellversuch) wird angelegt, sobald eine Instanz ihn beginnt, mit dem Zustand `Running`, und bei seiner Rückmeldung abgeschlossen. Steht ein Versuch ohne Ende auf `Running`, während keine Instanz ihn mehr ausführt, ist sein Ausgang unbekannt, wie bei einem Protokoll ohne Terminal-Status. So bleibt auch der letzte Versuch einer [Reaktion](../GLOSSARY.md#reaktion) stehen, die aufgegeben wurde, weil er sich nie zurückmeldete: Das Aufgeben selbst hält das Protokoll nicht fest. Ein leerer Zustand heisst, dass der Versuch seine Reaktion bei der Rückmeldung nicht mehr hielt; was er getan hat, sagt die Meldung.

## Lebendigkeit

Ob eine [Verarbeitung](../GLOSSARY.md#verarbeitung) noch läuft, sagt ihr [Protokoll](../GLOSSARY.md#protokoll), damit jede Instanz dieselbe Antwort gibt. Zu Ende ist eine Verarbeitung mit ihrem Terminal-Status, ein [Versuch](../GLOSSARY.md#versuch), dessen Lauf lieferbar endete, erst, wenn auch seine [Lieferung](../GLOSSARY.md#lieferung) erstellt oder `DeliveryFailure` gesetzt ist. Bis dahin frischt die Instanz, die die Verarbeitung ausführt, `HeartbeatAt` auf: zuerst mit dem Startdatensatz, danach alle 30 Sekunden, für alle ihre Verarbeitungen in einer Anweisung. Eine Verarbeitung, die nicht zu Ende ist, lebt, solange ihr Heartbeat jünger als drei Minuten ist; sonst ist sie tot und ihr [Ausgang](../GLOSSARY.md#ausgang) unbekannt. Ein Protokoll ohne Heartbeat entstand, bevor geopilot ihn festhielt; ohne Terminal-Status gilt seine Verarbeitung als tot.

Die drei Minuten sind die Frist zwischen zwei Heartbeats, nicht die erlaubte Dauer eines Schritts: Rechnet ein Schritt eine halbe Stunde, im Prozess oder im ilitools-wrapper, wird seine Verarbeitung in dieser Zeit weiter aufgefrischt. Geschrieben und verglichen wird mit der Uhr der Datenbank, damit Instanzen mit abweichender Uhr sich nicht gegenseitig für tot halten. Beim Herunterfahren schreibt eine Instanz ihre Heartbeats weiter, bis ihre laufenden Verarbeitungen zu Ende sind, auch wenn `Processing:ShutdownDrainTimeout` länger ist als drei Minuten.

Kann eine Instanz ihren Heartbeat nicht schreiben, bricht sie spätestens zweieinhalb Minuten nach dem letzten Erfolg, gemessen mit ihrer eigenen Uhr, alle Verarbeitungen ab, die sie hält, bevor die anderen Instanzen sie für tot halten. Der Heartbeat frischt zudem nur auf, was noch lebt, und eine Verarbeitung, die er nicht mehr auffrischen konnte, etwa nach einem eingefrorenen Container, bricht die Instanz ebenso ab. Tot heisst damit, dass niemand mehr an der Verarbeitung arbeitet und sich derselbe Upload neu starten lässt. Eine abgebrochene Verarbeitung endet als `Cancelled` und behält ihren Upload. Der Preis: Ein Datenbankausfall von mehr als zweieinhalb Minuten bricht alle laufenden Verarbeitungen ab.

Nach derselben Regel antworten die Lesepfade. Die Statusabfrage (`GET api/v2/processing/{jobId}`, für einen Versuch `GET api/v1/submission/{id}`) antwortet, solange die Verarbeitung lebt und, wenn sie zu Ende ist, bis `Processing:JobRetention` seit `TerminalAt` vergangen ist, auf jeder Instanz und auch nach einem Neustart; nur so lange nimmt geopilot auch die Abgabe an. Für eine tote Verarbeitung und nach diesem Fenster antwortet sie mit `404`, obwohl das Protokoll bleibt. Auch das Aufräumen entscheidet nach dem Protokoll und nicht nach dem Speicher der eigenen Instanz: Eine lebende Verarbeitung behält alle ihre Dateien, wo immer sie läuft, und als Ende einer toten gilt ihr letzter Heartbeat.

## Wo ist der Preflight?

Die Weboberfläche zeigt die Vorbereitung (Vollständigkeitsprüfung, Virenscan) als eigenen Schritt an, bei der Abgabe wie auf der Seite eines [Protokolls](../GLOSSARY.md#protokoll); das ist reine Darstellung. Im Protokoll ist der Preflight bewusst **keine** Schritt-Zeile, denn er steht in keiner [Pipeline-Definition](../GLOSSARY.md#pipeline) (und ein Kunde darf einen echten Schritt `preflight` nennen). Seine Ergebnisse liegen am Protokoll selbst: der Scan-Ausgang mit Details, die Datei-Hashes im Manifest, und bei einer Ablehnung der Terminal-Status `Failed` mit dem Grund (`IncompleteUpload: ...`, `SizeExceeded: ...`, `ThreatDetected: ...`).

Daraus lässt sich der Preflight-Zustand einer [Verarbeitung](../GLOSSARY.md#verarbeitung) ableiten:

- `Failed` und alle Schritte in `Pending`: im Preflight gescheitert, der Grund steht in `FailureReason`.
- `Cancelled` ohne `QueuedAt`: im Preflight abgebrochen, etwa weil geopilot sein Ende nicht ins Protokoll schreiben konnte oder die Instanz ihre Verarbeitungen abbrach (siehe [Lebendigkeit](#lebendigkeit)); der Grund steht in `FailureReason`.
- Kein Terminal-Status und kein `QueuedAt`: im Preflight, solange die Verarbeitung lebt, sonst dort durch einen Neustart unterbrochen.
- Kein Terminal-Status, `QueuedAt` gesetzt und alle Schritte `Pending`: wartet auf einen Platz in der Warteschlange, solange die Verarbeitung lebt, sonst dort unterbrochen.
- Ein Protokoll, das entstand, bevor geopilot `QueuedAt` festhielt, trennt die beiden letzten Fälle nur am Scan, und ohne ClamAV bleibt `ScanState` immer `NotScanned`. Beides ist ein Neustart, bevor der erste Schritt lief, und zählt gleich.

## Der Definitions-Snapshot

[Pipeline-Definitionen](../GLOSSARY.md#pipeline) sind nicht versioniert: nach einer Änderung der YAML existiert der alte Stand nicht mehr. Deshalb hält jedes [Protokoll](../GLOSSARY.md#protokoll) die Fassung fest, auf der seine [Verarbeitung](../GLOSSARY.md#verarbeitung) ausgeführt wurde, als JSON-Dokument (`jsonb`-Spalte `Definition`), aufgebaut wie eine minimale Definitionsdatei für genau diese eine Pipeline:

- `pipelines`: die gelaufene Pipeline mit allen Schritten, Inputs, Output-Actions und Conditions,
- `processes`: nur die von diesen Schritten referenzierten Katalogeinträge,
- `process_configs`: die wirksame Basis-Konfiguration (`Pipeline:ProcessConfigs`) dieser Implementierungen. Sie steht nicht in der YAML, entscheidet aber mit über das Verhalten, etwa `modelDirs`, also gegen welche Modell-Repositories eine INTERLIS-Validierung prüft. Übernommen werden nur Schlüssel, die einen Parameter des Prozesses treffen, und geheime Werte stehen nur als `**********` darin (siehe [Geheime Werte](pipeline/Pipelines.md#geheime-werte)). Einen vertippten Schlüssel, den geopilot beim Binden ohnehin ignoriert, zeigt der Snapshot deshalb nicht; lässt sich die Implementierung nicht auflösen, fehlt ihre Basis-Konfiguration ganz.

Nicht erfasst sind die Inhalte von `${file()}`-Ressourcen (welchen Stand ein Schritt bekam, zeigt der SHA-256 in seinen Eingaben), die Modell-Repositories hinter `modelDirs` und die Version der ilitools-wrapper. Der Snapshot beantwortet "auf welcher Grundlage lief das", er macht den Lauf nicht wiederholbar.

## Die Bindungen

Manches, was die Prozessoren einer Verarbeitung bekommen, steht nicht in der Definition, sondern wird beispielsweise im Admin-Portal gepflegt. Wenn ein Prozessor der Pipeline eine Bindung entgegennimmt, liest geopilot sie beim Start der Verarbeitung einmal und bindet sie daran; eine spätere Änderung erreicht eine gestartete Verarbeitung nicht mehr. Was gebunden wurde, hält die `jsonb`-Spalte `Bindings` fest, ein Objekt mit einem Schlüssel pro Bindung:

- `refMapping`: die [Zuordnung der Referenzdaten](Referenzdaten.md), alle Einträge, wie sie gebunden wurden (`scope`, `refDataUris`, `comment`). Jeder Validierungsschritt der Verarbeitung mit `refMappingFromPortal: true`, der lief, validierte mit genau diesen Einträgen; die übrigen Schritte ignorierten sie. Ist `RefMapping:Enabled` aus oder hat die Pipeline keinen Schritt, der diese Bindung annimmt, fehlt der Schlüssel. Ist der Schalter an, aber kein Eintrag erfasst, ist die Liste leer.

Ohne Bindung ist die Spalte `NULL`.

## Abfrage

Administratoren finden das Ausführungsprotokoll im Admin-Portal unter «[Protokolle](../GLOSSARY.md#protokoll)». Die Liste zeigt die Protokolle der Installation, neueste zuerst, mit Urheber, Mandat, Zustand, [Lieferung](../GLOSSARY.md#lieferung) und Dauer. Ist die maschinelle Lieferung eingerichtet, unterscheidet ein Symbol beim Urheber Benutzer:innen von Maschinen-Clients, und eine Spalte zählt die [Versuche](../GLOSSARY.md#versuch) einer Lieferung. Die Liste filtert nach Zeitraum, Urheber, Mandat und Zustand, bei Urheber, Mandat und Zustand auch nach mehreren zugleich; «Nicht abgeschlossen» findet die Protokolle ohne Terminal-Status. Eine Verarbeitungs-ID im Suchfeld öffnet ihr Protokoll direkt, auch wenn nie eine Lieferung daraus wurde, und die Seite einer Lieferung verlinkt ihr Protokoll. Die Seite eines Protokolls fasst es zusammen. Hatte seine Lieferung mehrere Versuche, listet sie darüber alle Versuche mit Start, Zustand und Lieferung, und ein Klick auf einen Versuch öffnet seine Seite. Unter der Zusammenfassung zeigt die Seite in Tabs den Ablauf, die Ereignisse der [Reaktionen](../GLOSSARY.md#reaktion) und die [Pipeline](../GLOSSARY.md#pipeline), wie sie beim Start konfiguriert war, lesbar oder als JSON. Die Ereignisse der Reaktionen stehen wie die Schritte im Ablauf untereinander, jedes mit der Reaktion (ihr Name aus der Pipeline-Definition, wie sie jetzt ist, daneben ihre Id), ihrer Art und was geschah: ein [Zustellversuch](../GLOSSARY.md#zustellversuch) mit seiner Nummer, wann er begann und wie lange er lief oder dass er noch läuft, eine Rücksetzung durch einen Admin oder eine Löschung mit ihrem Zeitpunkt. Ein Symbol zeigt den Ausgang eines Zustellversuchs, bei einer Rücksetzung oder Löschung den Zustand der Reaktion danach. Ein gescheiterter Zustellversuch erscheint als gescheitert, auch wenn noch ein weiterer folgt. Wartet eine Reaktion auf ihren nächsten Zustellversuch, steht dafür noch kein Eintrag da; wann er fällig ist, zeigt die Seite der Lieferung. Ein Zustellversuch lässt sich aufklappen und zeigt seinen Kanal mit der geopilot-Version, die Konfiguration und die Meldung. Der Ablauf zeigt wie bei der Abgabe zuerst die Vorbereitung mit den hochgeladenen Dateien, dem Virenscan und, wenn sie scheiterte, dem Grund, dann die Schritte. Endete die [Verarbeitung](../GLOSSARY.md#verarbeitung) sonst ausserhalb der Schritte, etwa im Timeout, steht der Grund darüber. Bei den Eingaben eines Schritts trägt eine gelieferte Datei den Hash aus der Vorbereitung, auch wo ein früherer Schritt sie unverändert weitergereicht hat: Gleicher Hash heisst, der Schritt bekam genau die gelieferte Datei. Eine Datei, die erst in der Verarbeitung entstand, hat keinen Hash und heisst dort «in der Verarbeitung erzeugt».

Was aus einer Verarbeitung wurde, steht nicht im Protokoll. Die Ansicht leitet es beim Lesen ab: aus dem Protokoll, aus den Lieferungen und daraus, ob die Verarbeitung noch lebt (siehe [Lebendigkeit](#lebendigkeit)), auf welcher Instanz auch immer sie läuft. Ob eine Lieferung entstehen konnte, entscheiden zwei unabhängige Fragen: ob die Daten sie zuliessen, die Verarbeitung also in `Success` oder `Warning` endete, und ob, wer ihn startete, auf dem Mandat abgeben durfte (`PermissionToDeliver`).

- Die Id der Lieferung, die für die Verarbeitung erstellt wurde; «(gelöscht)», wenn sie wieder gelöscht wurde.
- «nicht möglich»: Die Daten liessen keine Lieferung zu, gleich wer die Verarbeitung startete. Der Zustand nennt die Schritte, die die Lieferung einschränkten, scheiterten oder abgebrochen wurden, oder die Vorbereitung, wenn der Preflight scheiterte. Endete die Verarbeitung ausserhalb der Schritte, nennt er keinen.
- «nicht berechtigt»: Die Daten liessen eine Lieferung zu, aber wer die Verarbeitung startete, durfte nicht abgeben, etwa nach einem anonymen Start auf einem öffentlichen Mandat.
- «nicht abgegeben»: Die Daten liessen eine Lieferung zu, und wer die Verarbeitung startete, durfte abgeben, versucht hat es aber niemand.
- «gescheitert»: Die Daten liessen eine Lieferung zu, und wer die Verarbeitung startete, durfte abgeben, aber geopilot wies die Lieferung ab oder konnte sie nicht erstellen, bei einer Abgabe im Web wie bei einer maschinellen Lieferung. Das Protokoll hält den Grund fest, die Seite des Protokolls nennt ihn (`DeliveryFailure`). Gelingt ein späterer Versuch, steht stattdessen die Lieferung da.
- Keine Angabe: Die Verarbeitung läuft noch, oder ihr [Ausgang](../GLOSSARY.md#ausgang) ist unbekannt.

Bei einem Protokoll ohne `PermissionToDeliver` gilt nur ein anonymer Start als nicht berechtigt; die Seite des Protokolls weist darauf hin.

Dieselben Angaben liefert die API:

- `GET api/v1/recording`: die Protokolle seitenweise, neueste zuerst, gefiltert nach Zeitraum (`from`, `to`), Mandaten (`mandateIds`), Urhebern (`userIds`, `machineClientIds`) und Zuständen (`states`). Ein Protokoll passt zu einem Filter mit mehreren Werten, wenn er zu einem davon passt. Die Versuche einer Lieferung bilden eine Gruppe und zählen beim Blättern als eine Zeile; ein Protokoll ohne `ClientReference` ist eine Gruppe für sich. Eine Gruppe erscheint, wenn einer ihrer Versuche im Zeitraum liegt, der Zustand gilt ihrem neuesten Versuch, und die Zahl ihrer Versuche (`attemptCount`) zählt auch die ausserhalb des Zeitraums.
- `GET api/v1/recording/{jobId}`: das Protokoll mit Manifest, Schritten samt ihren Eingaben (`inputs`), Condition-Auswertungen, Artefaktnamen, der Lieferberechtigung (`permissionToDeliver`), dem Grund eines Scheiterns, das kein Schritt nennt (`failureReason`), dem Grund einer gescheiterten Lieferung (`deliveryFailure`) und den [Zustellversuchen](../GLOSSARY.md#zustellversuch) der Reaktionen (`reactionEvents`, jedes mit dem Namen der Reaktion nach der aktuellen Definition, `reactionDisplayName`, ein Zustellversuch dazu mit `appVersion` und `config`), dazu Urheber und Mandat mit Namen (`startedBy`, `mandate`), was aus der Verarbeitung wurde (`outcome`, mit `preflightFailed`, wenn sie im Preflight scheiterte), die Zustände der [Weitergabe](../GLOSSARY.md#weitergabe) und der [Benachrichtigungen](../GLOSSARY.md#benachrichtigung) seiner Lieferung (`forwardingState`, `notificationState`) und die Versuche der Lieferung, neueste zuerst und das Protokoll selbst darunter (`attempts`).
- `GET api/v1/recording/{jobId}/definition`: der Definitions-Snapshot als JSON.

Für Auswertungen über mehrere Protokolle ist SQL das Werkzeug. Beispiele:

```sql
-- Welche Pipeline-Fassung lief für Delivery X, welche Schritte, warum?
SELECT r."PipelineId", s."StepId", s."State", s."ErrorMessage", c."ConditionId", c."Matched", c."EvaluatedValues"
FROM "Deliveries" d
JOIN "Recordings" r ON r."JobId" = d."JobId"
JOIN "RecordingSteps" s ON s."RecordingId" = r."Id"
LEFT JOIN "RecordingConditions" c ON c."RecordingStepId" = s."Id"
WHERE d."Id" = 42
ORDER BY s."Order";

-- Womit arbeiteten die Schritte von Delivery X?
SELECT s."StepId", i."Parameter", i."Origin", i."Reference", i."Kind", i."Value", i."FileName", i."Size", i."Sha256"
FROM "Deliveries" d
JOIN "Recordings" r ON r."JobId" = d."JobId"
JOIN "RecordingSteps" s ON s."RecordingId" = r."Id"
JOIN "RecordingInputs" i ON i."RecordingStepId" = s."Id"
WHERE d."Id" = 42
ORDER BY s."Order", i."Id";

-- Welche Verarbeitungen bekamen eine Ressource in welchem Stand?
SELECT r."JobId", r."StartedAt", s."StepId", i."Sha256"
FROM "RecordingInputs" i
JOIN "RecordingSteps" s ON s."Id" = i."RecordingStepId"
JOIN "Recordings" r ON r."Id" = s."RecordingId"
WHERE i."Origin" IN ('Resource', 'Configuration') AND i."Reference" = 'model-repository.zip'
ORDER BY r."StartedAt";

-- Welche Verarbeitungen nutzten eine bestimmte Prozessor-Implementierung?
SELECT r."JobId", r."StartedAt"
FROM "Recordings" r
WHERE r."Definition" @> '{"processes": [{"implementation": "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess"}]}';

-- Welche Referenzdaten bekam Delivery X für ihren Scope?
SELECT e ->> 'scope' AS "Scope", e -> 'refDataUris' AS "RefDataUris"
FROM "Deliveries" d
JOIN "Recordings" r ON r."JobId" = d."JobId"
CROSS JOIN jsonb_array_elements(r."Bindings" -> 'refMapping') e
WHERE d."Id" = 42 AND e ->> 'scope' = '449';

-- Wie viele Protokolle blieben ohne Terminal-Status (Restart-Opfer)?
SELECT count(*) FROM "Recordings"
WHERE "TerminalState" IS NULL AND ("HeartbeatAt" IS NULL OR "HeartbeatAt" < now() - interval '3 minutes');

-- Lieferbare Verarbeitungen ohne Lieferung: nicht berechtigt, gescheitert oder nie abgegeben?
SELECT r."JobId", r."StartedAt", r."PermissionToDeliver", r."DeliveryFailure"
FROM "Recordings" r
WHERE r."TerminalState" IN ('Success', 'Warning')
  AND NOT EXISTS (SELECT 1 FROM "Deliveries" d WHERE d."JobId" = r."JobId");

-- Protokolle gruppiert nach identischer Definitions-Fassung.
SELECT "PipelineId", "Definition", count(*), min("StartedAt"), max("StartedAt")
FROM "Recordings"
GROUP BY "PipelineId", "Definition";

-- Alle Versuche einer maschinellen Lieferung, ältester zuerst.
SELECT "JobId", "StartedAt", "TerminalState"
FROM "Recordings"
WHERE "ClientReference" = 'LIEF-2026-0815' AND "MachineClientId" = 3 AND "MandateId" = 7
ORDER BY "StartedAt";

-- Wie wurde Delivery X weitergegeben?
SELECT e."ReactionId", e."Attempt", e."OccurredAt", e."FinishedAt", e."State", e."Message" ->> 'de' AS "Message"
FROM "RecordingReactionEvents" e
WHERE e."DeliveryId" = 42
ORDER BY e."OccurredAt", e."Id";
```

## Aufbewahrung

`Processing:ProtocolRetention` (Standard: 3650 Tage) bestimmt, wie lange Protokoll-Datensätze aufbewahrt werden, unabhängig von der viel kürzeren `Processing:JobRetention`. Der Aufräumdienst löscht ältere [Protokolle](../GLOSSARY.md#protokoll) samt Kind-Datensätzen. Ist der Schlüssel nicht gesetzt, wird nie gelöscht: ein fehlender Konfigurationswert kann das Protokoll nicht stillschweigend leeren. Die Frist ist mit den Datenschutz-Anforderungen des Betreibers abzustimmen, das Protokoll enthält Benutzerreferenzen und Dateinamen, und mit der `ClientReference` einen Freitext, den ein Client für seine [Lieferungen](../GLOSSARY.md#lieferung) wählt. Die Konfiguration eines Zustellversuchs enthält die Werte des Mandats, beim Mail-Kanal die Adressen der Empfänger; auch sie gehören zu den Personendaten. Weist ein Mailserver einen Empfänger ab, nennt die Meldung des Zustellversuchs dessen Adresse, mit `includeSupplier` womöglich die des Lieferanten.

## Dateien

Das [Protokoll](../GLOSSARY.md#protokoll) hält **keine** Dateien, nur Metadaten und eine credential-freie Referenz: den Ablageort des Upload-Storage (`UploadStorageLocation`) plus den Storage-Key pro Datei. Wie lange die Dateien selbst existieren, bestimmen die bestehenden Regeln: Uploads werden gelöscht, sobald ihr Lauf endet, ausser bei einem abgebrochenen Lauf, der sie bis zum Ablauf der Upload-Session behält. Downloads und Visualisierungen verschwinden nach ihrer kurzen Retention, Lieferdateien einer [Verarbeitung](../GLOSSARY.md#verarbeitung), die jemand abgeben darf, warten bis zur Abgabe unter `assets/staging/<jobId>` und verschwinden mit der Verarbeitung nach der `JobRetention`, wenn keine [Lieferung](../GLOSSARY.md#lieferung) erstellt wird; alle drei Fristen zählen ab dem Ende des Laufs, bei einer toten Verarbeitung ab ihrem letzten Heartbeat; Lieferdateien einer eingereichten Lieferung bleiben als Assets erhalten (erreichbar über den Join `Deliveries.JobId = Recordings.JobId`).

Der SHA-256 im Manifest beweist die Identität einer Datei, wenn sie erneut vorgelegt wird. Sollen die referenzierten Uploads selbst über die geopilot-Fristen hinaus verfügbar bleiben, empfiehlt sich Soft Delete oder Versioning auf dem Storage-Container: dann zeigt die Referenz auch nach dem Löschen durch geopilot auf wiederherstellbare Blobs. Das ist Hosting-Konfiguration und liegt beim Betreiber.
