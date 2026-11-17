# Konfiguration der Zustellversuche festhalten und geheime Werte maskieren (#1126)

Review: required. Sicherheitsrelevant: Es entscheidet, ob Zugangsdaten in die Datenbank gelangen. Dazu kommt ein öffentlicher Typ in PipelineCore, auf dem Plugins aufbauen. Die Migration legt nur Spalten an.

Abhängig von: #1188. Die Umsetzung beginnt nach dessen Merge und kommt in einem PR: Migration und GUI aus #1188 liegen sonst daneben, und ein Teil-PR ohne GUI müsste #1188 zum Nachziehen seiner Migration zwingen. Benennt #1195 die Tabellen vorher um, gelten die neuen Namen.

Ein Zustellversuch soll später belegen, womit und wohin sein Kanal lief. Heute nennt `PipelineRunReactionEvents` nur Kanal, Versuch, Zustand und Meldung. `DeliveryReaction.Config` hilft nicht weiter: Dort fehlen die Basis-Konfiguration (`Pipeline:ChannelConfigs`) und der Stand der Ressourcen, die jeder Versuch neu liest, und sobald die Lieferung gelöscht ist, blendet ein Query-Filter die Reaktion aus. Zugleich übernimmt der Definitions-Snapshot eines Laufs `Pipeline:ProcessConfigs` im Klartext, für `Processing:ProtocolRetention` (Standard 10 Jahre). Heute steht dort kein Secret, mit dem ersten Plugin, das einen Token braucht, schon. Beides gehört zusammen: Hält das Protokoll die Konfiguration der Versuche fest, muss das Passwort des Mail-Kanals maskiert sein.

## Umsetzung

**Kennzeichnung.** `[SecretConfigValue]` kommt als öffentliches Attribut für Konstruktor-Parameter nach `Geopilot.PipelineCore`, neben `[PipelineProcessRun]`. PipelineCore 2.0 ist noch nicht veröffentlicht, die Version bleibt; erscheint 2.0 vorher, wird es 2.1. Es gilt nur für Parameter vom Typ `string`, an einem anderen Typ scheitert der Start: Die Basis-Konfiguration trägt ohnehin nur Text, und Text scheitert nie an der Konvertierung, deren Fehlermeldung den Wert nennen würde (`ConstructorBinding.TryGetConfiguredValue`). Ein gekennzeichneter Parameter nimmt seinen Wert nur aus der Basis-Konfiguration:

- Prozesse: Setzt die Definition ihn in `default_config` oder `process_config_overwrites`, scheitert der Start, auch wenn die Basis-Konfiguration ihn nicht setzt. Geprüft werden wie bei der Kollisionsregel alle Einträge unter `processes`, auch solche, die kein Schritt verwendet; die Startprüfung pro Schritt sieht diese nicht. Die Kollisionsregel selbst greift nur, wenn beide Schichten den Schlüssel setzen.
- Kanäle: `DeliveryChannelFactory.Validate` behandelt `[SecretConfigValue]` wie das interne `[BaseConfigValue]`. Trägt ein Parameter zugleich `[MandateValue]`, scheitert der Start.
- `[BaseConfigValue]` bleibt intern für Werte, die nur der Betrieb setzt, die aber nicht geheim sind, etwa Server und Ablageverzeichnis. Das Passwort des Mail-Kanals trägt `[SecretConfigValue]` statt `[BaseConfigValue]`. Die eingebauten Prozesse haben keinen geheimen Parameter.

**Maskieren.** Ein Helfer neben `ConstructorBinding` ersetzt den Wert jedes gekennzeichneten Parameters durch `**********`, sobald er gesetzt ist; der Schlüssel bleibt, damit sichtbar ist, dass ein Wert da war. Ein Wert, der nicht gesetzt ist, fehlt. Maskiert wird beim Schreiben, an beiden Stellen unten, nie beim Lesen. Damit erreicht kein gekennzeichneter Wert eine Spalte des Protokolls, auch nicht die Meldung eines Versuchs: Die eingebauten Kanäle nennen ihn in keiner Ausnahme, und die Doku für Plugin-Autoren verlangt dasselbe.

**Definitions-Snapshot.** `PipelineFactory.GetDefinitionSnapshotJson` übernimmt unter `process_configs` nur noch Schlüssel, die einen Konstruktor-Parameter der Implementierung treffen, und maskiert die gekennzeichneten. Unbekannte Schlüssel ignoriert geopilot beim Binden, kopiert sie heute aber in den Snapshot; ein vertippter Schlüssel brächte so ein Secret im Klartext in die Datenbank. Dass ein vergessener Schlüssel im Snapshot nicht mehr zu sehen ist, nehmen wir dafür in Kauf. Die Parameter kommen vom Typ, den `PipelineProcessFactory` wie bei der Startprüfung auflöst; `PipelineFactory` kennt heute nur das öffentliche `IPipelineProcessFactory`, wie sie an den Typ kommt, bleibt der Umsetzung. Lässt sich der Typ nicht auflösen, fehlen die Schlüssel dieser Implementierung im Snapshot ganz.

**Konfiguration pro Zustellversuch.** `DeliveryChannelFactory` stellt vor jedem Versuch die wirksame Konfiguration zusammen: pro Konstruktor-Parameter der Wert aus der ersten Quelle, aus der `Create` bindet, und diese Quelle als `origin`: `base` (Basis-Konfiguration), `definition` (`default_config` des Kanals mit den `channel_config_overwrites` der Reaktion, bei der Deklaration eingefroren) oder `mandate` (nur für `[MandateValue]`). Die Quellen überschneiden sich nie, das verhindern die Kollisionsregeln; zusammengeführt steht jeder Schlüssel an einer Stelle, mit seiner Herkunft. Die Wahl der Quelle steht danach an einer Stelle, für das Binden und das Festhalten; Lesen und Konvertieren bleiben beim Binden, denn dort wirft eine fehlende Datei, und das Festhalten soll gerade dann gelingen. Werte stehen da, wie sie konfiguriert sind, nicht konvertiert. Gekennzeichnete sind maskiert. Bei einem Parameter mit `[ResourceFile]` ist `value` der Pfad, wie konfiguriert, dazu kommt `sha256`, der Hash des Inhalts beim Versuch; `sha256` ist `null`, wenn die Datei fehlt oder ausserhalb des Ressourcenverzeichnisses liegt. Für den Mail-Kanal etwa:

```json
{
  "host": { "value": "smtp.example.com", "origin": "base" },
  "port": { "value": "587", "origin": "base" },
  "sender": { "value": "geopilot@example.com", "origin": "base" },
  "username": { "value": "geopilot", "origin": "base" },
  "password": { "value": "**********", "origin": "base" },
  "template": { "value": "mail/info.html", "origin": "definition", "sha256": "9f86d081..." },
  "to": { "value": ["empfang@example.com"], "origin": "mandate" },
  "includeDeclarer": { "value": true, "origin": "mandate" }
}
```

`ReactionWorker` gibt das Ergebnis an `RecordReactionAttemptStartedAsync`, also schon an die erste Zeile des Versuchs, bevor der Kanal gebaut wird. So zeigt auch ein unterbrochener Versuch seine Konfiguration, und einer, dessen Kanal einen Wert ablehnt, den abgelehnten Wert. Legt `RecordReactionAttemptFinishedAsync` die Zeile nachträglich an, trägt sie dieselbe Konfiguration. Lässt sich nichts zusammenstellen, etwa weil der Kanal unbekannt ist, bleibt `Config` `null`, und die Meldung sagt warum.

**Speichern.** `PipelineRunReactionEvents` bekommt zwei Spalten: `AppVersion`, die Version der Instanz, die den Versuch ausführt (zwischen zwei Versuchen kann ein Update liegen), und `Config` als `jsonb`. Das Issue nennt eine Spalte; die Version steht wie an `PipelineRuns` in einer eigenen, damit sie sich per SQL gleich abfragen lässt. Beide sind nur bei `Attempt` gesetzt, bei `Reset`, `Deletion` und älteren Versuchen `null`; `AppVersion` auch dann, wenn `Config` `null` bleibt. Die Migration legt nur die Spalten an.

**API und GUI.** `PipelineRunReactionEventResponse` bekommt `appVersion` und `config`, das JSON-Objekt wie gespeichert; die generierten Typen kommen aus `npm run openapi`. Die Konfiguration erscheint im Reiter *Reaktionen* der Detailansicht aus #1188, nicht im Reiter *Pipeline*. Dieser zeigt den Stand beim Start, einen pro Verarbeitung; der Reiter *Ablauf* dagegen zeigt, was ein Schritt beim Laufen bekam. Die Konfiguration eines Versuchs ist von dieser zweiten Art, denn jeder Versuch liest Basis-Konfiguration und Ressourcen neu, und zwei Versuche können verschieden laufen. In der Tabelle der Ereignisse lässt sich ein Versuch aufklappen und zeigt darunter die Version und eine Tabelle *Konfiguration* im Format der *Eingabe* im Ablauf: Parameter, Wert, Quelle und SHA-256. Die Quelle heisst «Basis der Installation» oder «Pipeline-Definition», wie im Reiter *Pipeline*, oder «Mandat». Aufklappen lässt sich jeder Versuch mit `appVersion`, also jeder ab dieser Version, auch einer ohne `config`; dann steht dort nur die Version.

**Doku.**

- `docs/Ausfuehrungsprotokoll.md`: unter *Was aufgezeichnet wird* `AppVersion` und `Config` der Versuche; der Satz, dass Werte des Mandats dort nicht stehen, fällt weg. Unter *Der Definitions-Snapshot*: nur Schlüssel, die einen Parameter treffen, gekennzeichnete maskiert. Unter *Aufbewahrung*: Die Adressen der Empfänger gehören zu den Personendaten, und die Meldung zu einem abgelehnten Empfänger nennt dessen Adresse, mit `includeDeclarer` womöglich die des Lieferanten.
- `docs/pipeline/Pipelines.md`: unter *Kollision mit der Basis-Konfiguration* und bei den Kanälen, dass ein gekennzeichneter Parameter nur aus der Basis-Konfiguration kommt und maskiert festgehalten wird.
- `docs/pipeline/pluginSystem.md`: `[SecretConfigValue]` für Plugin-Autoren, mit Beispiel: nur für `string`, Wert nur aus der Basis-Konfiguration, und eine Ausnahme des Konstruktors nennt ihn nicht.
- `docs/pipeline/Kanaele/mail.md`: Das Passwort ist gekennzeichnet und steht maskiert im Protokoll.
- CHANGELOG: ein Eintrag unter Added.

## Nicht im Umfang

- `DeliveryReaction.Config` bleibt die bei der Deklaration eingefrorene Eingabe ohne Basis-Konfiguration (ADR 0015); ein Versuch liest Definition und Mandat weiterhin nicht neu.
- Keine Datenmigration: Frühere Versuche bleiben ohne Konfiguration, und heute steht in keiner Installation ein Secret in `Pipeline:ProcessConfigs`.
- Kein Maskieren beim Lesen, weder in API noch GUI: Was nie gespeichert wird, muss keine Lesestelle verbergen.
- Die Adresse des Lieferanten: `includeDeclarer` steht als Schalter da, die Adresse liest jeder Versuch aus der Lieferung.
- Was ein Kanal tatsächlich geschrieben hat (Pfad, Topic, Offset), steht weiterhin nur in seiner Meldung.
- Ein Lauf oder Versuch lässt sich aus dem Protokoll nicht wiederholen (ADR 0010); eine Wiederholung nähme die Secrets aus der aktuellen Konfiguration.
- Unbekannte Schlüssel in der Basis-Konfiguration lassen den Start weiterhin nicht scheitern.

## Prüfung

- `Geopilot.Pipeline.Test`: Ein gekennzeichneter Prozess-Parameter in `default_config` oder `process_config_overwrites` lässt die Startprüfung scheitern, auch ohne Eintrag in der Basis-Konfiguration und bei einem Prozess, den kein Schritt verwendet; bei einem Kanal ebenso, auch in `channel_config_overwrites`. `[SecretConfigValue]` mit `[MandateValue]` oder an einem anderen Typ als `string` scheitert. Der Snapshot maskiert einen gekennzeichneten Wert und lässt einen unbekannten Schlüssel weg. Die zusammengestellte Konfiguration eines Kanals nimmt jede Quelle in derselben Reihenfolge wie `Create`, nennt zu jedem Wert diese Quelle, maskiert und hält eine Ressource mit Pfad und Hash fest, bei fehlender Datei mit `sha256` `null`.
- `Geopilot.Api.Test`: Ein Test-Kanal mit gekennzeichnetem Parameter und einem unverwechselbaren Wert läuft einen Versuch durch, auch einen, dessen Konstruktor einen anderen Wert ablehnt. Die erste Zeile des Versuchs trägt `Config` und `AppVersion`, und der Wert steht in keiner Spalte der Protokolltabellen. Reset und Löschung haben keine Konfiguration.
- Von Auge im Dev-Stack: Eine Mail-Reaktion gegen `mailpit-tls` zeigt im Reiter *Reaktionen* aufgeklappt `password` als `**********` aus der Basis der Installation, das Template mit Pfad und Hash aus der Pipeline-Definition und die Empfänger aus dem Mandat.
- `dotnet tool run dotnet-ef migrations has-pending-model-changes` meldet nichts, CI ist grün, auch der Abgleich der generierten Typen.
