# Eine tote Verarbeitung meldet 404 statt eines Zustands (#891)

## Begriffe

- **Tot** (neu): Eine Verarbeitung, an der keine Instanz mehr arbeitet, bevor sie fertig ist. Ihr letzter Heartbeat ist älter als die Schwelle, und ihr fehlt der Endzustand oder, bei einem Versuch mit lieferbarem Lauf, die Lieferung oder der Grund in `DeliveryFailure`. Meist hat geopilot hart abgebrochen, etwa wegen Speichermangels oder eines ausgefallenen Servers. Vermeiden: verloren, hart abgeschossen (das ist die Ursache, nicht der Zustand).
- **Ausgang:** bekommt den Wert unbekannt (`unknown`) für eine tote Verarbeitung. Er wird nicht gespeichert, sondern beim Lesen abgeleitet.

## Umsetzung

**Lesepfade.** Ob eine Verarbeitung tot ist, sagt die bestehende Regel auf dem Lauf. Für eine tote Verarbeitung antworten die Statusabfrage `GET api/v2/processing/{jobId}` und der Status eines Versuchs `GET api/v1/submission/{id}` neu mit 200 statt 404:

- Die Statusabfrage meldet `cancelled` mit `runOutcome` `unknown`. Die Schritte zeigt sie, wie das Protokoll sie hält, samt den Downloads und Visualisierungen, deren Retention noch läuft. Ein Schritt, der dort auf `Running` steht, erscheint als `Cancelled`.
- Hat die Verarbeitung schon einen Endzustand, weil ihre Instanz zwischen dem Ende des Laufs und der Lieferung starb, meldet die Statusabfrage diesen Endzustand mit seinem Ausgang.
- Der Status eines Versuchs meldet in beiden Fällen `failed`. Die Ableitung in `SubmissionController.DeriveState` bekommt dafür einen Zweig für die tote Verarbeitung, denn ein lieferbarer Lauf ohne Lieferung meldet heute `processing`. `messages` nennt den Grund: Die Verarbeitung wurde unterbrochen, eine Lieferung ist nicht entstanden.

Beide antworten so bis `Processing:JobRetention` nach dem letzten Heartbeat, ohne Heartbeat nach dem Start. Das ist dasselbe Ende, nach dem das Cleanup die Verzeichnisse der Verarbeitung aufräumt. Danach und für eine unbekannte Id bleibt es bei 404. An der Abgabe ändert sich nichts.

**Vorbereitung.** Die Statusabfrage meldet die Vorbereitung nur als bestanden, wenn die Verarbeitung in die Warteschlange kam (`QueuedAt`). Endete sie vorher mit `Cancelled` oder ist sie vorher tot, steht die Vorbereitung auf `Cancelled`. Das betrifft auch eine Verarbeitung, die das Herunterfahren im Preflight beendet hat; heute gilt jede abgebrochene Verarbeitung als vorbereitet.

`Cancelled` statt eines neuen Zustands: `ProcessingState` gehört zu `Geopilot.Pipeline` und damit zum Vertrag der Plugins, und `Cancelled` heisst schon heute, dass es nicht an den Daten lag und der Upload bleibt. Was eine tote Verarbeitung von einer abgebrochenen unterscheidet, sagt der Ausgang.

**Wizard.** Bei `cancelled` mit `unknown` zeigt `deliveryProvider.tsx` eine eigene Meldung: Die Verarbeitung wurde unterbrochen, ihr Ergebnis ist unbekannt, und der Lieferant muss von vorn beginnen. Die Abfrage endet wie bei jedem Endzustand, ein erneuter Start folgt nicht. Der Text kommt auf Deutsch, Englisch, Französisch und Italienisch in `public/locale`.

**Doku.** Swagger nennt `unknown` beim Feld `runOutcome`, und das 404 der Statusabfrage nur noch für eine unbekannte Id oder ein abgelaufenes Fenster. In `docs/MaschinelleLieferung.md` meldet unter "Lebensdauer" ein Versuch, dessen Verarbeitung tot ist, `failed` bis `JobRetention` nach ihrem letzten Heartbeat; unter "Antworten" heisst `404` nur noch, dass der Versuch unbekannt oder sein Fenster abgelaufen ist. CHANGELOG unter Changed: Für eine tote Verarbeitung, wie sie ein harter Abbruch von geopilot hinterlässt, meldet die Statusabfrage `cancelled` mit dem Ausgang `unknown` statt 404, und ein Versuch meldet `failed`.

## Prüfung

- Lesepfad als Tabellentest: tot im Fenster (200, `cancelled`, `unknown`, ein Schritt auf `Running` als `Cancelled`), tot nach dem Fenster (404), unbekannte Id (404), tot mit lieferbarem Endzustand, aber ohne Lieferung (dieser Endzustand), lebend (wie bisher).
- Vorbereitung: `Cancelled` oder tot ohne `QueuedAt` meldet sie als `Cancelled`, mit `QueuedAt` als bestanden.
- Status eines Versuchs: tot ohne Endzustand und tot mit lieferbarem Lauf ohne Lieferung melden im Fenster `failed`, mit dem Grund in `messages`.
- Cypress mit Intercept: `cancelled` mit `unknown` zeigt die eigene Meldung, und es folgt kein Start.
- Smoke-Test: Die Instanz während eines Laufs hart beenden, etwa mit `docker kill`, und wieder starten. Drei Minuten nach dem letzten Heartbeat zeigt der Wizard die Meldung, und ein Maschinen-Client bekommt `failed`.
