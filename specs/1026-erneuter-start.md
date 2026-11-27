# Eine Verarbeitung, die das Herunterfahren abbricht, startet im Wizard erneut (#1026)

## Begriffe

- **Wizard** (neu im Glossar): Die Startseite «Lieferung», auf der eine Person im Web Dateien hochlädt, ein Mandat wählt, die Verarbeitung abwartet und, wenn sie angemeldet ist, die Lieferung abgibt. Welche Verarbeitung er verfolgt, weiss nur der Browser. Im Code: `pages/delivery`, der Zustand in `deliveryProvider.tsx`.
- **Erneuter Start** (neu): Der Wizard startet für denselben Upload und dasselbe Mandat eine neue Verarbeitung, nachdem das Herunterfahren die vorige abgebrochen hat. Die neue hat ihre eigene Id und ihr eigenes Protokoll, die abgebrochene bleibt, wie sie ist. Der erneute Start entspricht dem Wiederholen eines Maschinen-Clients nach `failed`: Wo dieser einen weiteren Versuch schickt, startet der Wizard eine weitere Verarbeitung. Im Code: im Wizard (`deliveryProvider.tsx`). Vermeiden: Wiederaufnahme (die setzte den Lauf fort), Wiedereinreichung, Neustart (so heissen der von geopilot und im Wizard der Button, der die Lieferung zurücksetzt), erneuter Versuch und Retry (was der Wizard neu startet, ist kein Versuch, sondern eine Verarbeitung; zudem heisst der Zustellversuch in der Oberfläche kurz Versuch, und «Erneut versuchen» setzt dort eine aufgegebene Reaktion zurück).
  _Übersetzungen_: EN rerun, FR un nouveau lancement, IT un nuovo avvio
- **Laufausgang** (umbenannt, bisher Ausgang): Der neue Name passt zu `RunOutcome` und lässt «Ausgang» frei, das die Doku auch für Zustellversuche und den Virenscan braucht. Neu steht der Laufausgang im Protokoll und in der Statusantwort, als Spalte `RunOutcome` und als Feld `runOutcome`. Gegenüber dem Glossar kommt die Unterbrechung dazu (`Interrupted`), und ein Schritt, der wirft, ist kein Absturz, sondern zählt wie eine Bedingung, die die Daten scheitern lässt. Nicht zu verwechseln mit `outcome` der Ansicht eines Protokolls, das sagt, was aus der Verarbeitung wurde.

## Umsetzung

**Laufausgang im Protokoll.** `Recordings` bekommt die Spalte `RunOutcome`, ein Enum mit einem Wert je Art von `RunOutcome`, gespeichert als Text wie die übrigen Enums. Geschrieben wird sie mit jedem Endzustand ausser dem eines gescheiterten Preflights, also auch, wenn das Herunterfahren oder die Datenbank eine Verarbeitung vor ihrem Lauf beendet. `FailureReason` bleibt als Text für Menschen.

| Wert | Wann | Wizard |
|---|---|---|
| `Completed` | Die Pipeline lief zu Ende, auch wenn eine Bedingung die Daten scheitern liess oder ein Schritt warf. Das Ergebnis sagt der Endzustand. | kein erneuter Start |
| `TimedOut` | Der Lauf überschritt `Processing:JobTimeout`. | kein erneuter Start |
| `ShutDown` | Das Herunterfahren beendete die Verarbeitung, im Lauf oder davor. | erneuter Start |
| `Crashed` | Eine Exception ausserhalb der Schritte brach den Lauf ab, etwa beim Speichern der Downloads oder beim Stagen der Lieferdateien. | kein erneuter Start |
| `Interrupted` | Die Datenbank war zu lange nicht erreichbar, im Lauf oder schon im Preflight. | kein erneuter Start |
| leer | Die Verarbeitung hat keinen Endzustand, ihr Preflight scheiterte, oder sie endete vor der Migration. | kein erneuter Start |

Die Migration fügt nur die Spalte hinzu, ältere Zeilen bleiben leer. Gelesen wird der Laufausgang nur von der Statusabfrage, die Ansicht eines Protokolls zeigt ihn nicht. Dass leer mehreres heissen kann, stört dort nicht: Ob die Verarbeitung noch läuft, sagt ihr Zustand, und eine beendete ohne Laufausgang startet der Wizard nicht erneut. Das gilt auch für die Verarbeitungen, die das Deployment mit der Migration abbricht.

**Statusantwort.** `ProcessingJobResponse` bekommt `runOutcome` aus dieser Spalte, mit denselben Werten in camelCase, `null` ohne Laufausgang, und Swagger beschreibt das Feld. Die Statusabfrage liest es wie alles andere aus dem Protokoll, also auch nach einem Neustart von geopilot.

**Erneuter Start.** Endet die Abfrage in `deliveryProvider.tsx` mit `cancelled` und `runOutcome` `shutDown`, startet der Wizard mit `uploadId` und dem gewählten Mandat über `POST api/v2/processing` eine neue Verarbeitung, statt die Meldung für eine abgebrochene zu zeigen. Er bleibt auf dem Schritt der Verarbeitung, übernimmt die neue Id für Abfrage, Downloads und Abgabe und zeigt den Hinweis «Die Verarbeitung wurde wegen Wartungsarbeiten an geopilot abgebrochen und mit denselben Dateien erneut gestartet.»

Nach zwei erneuten Starts zeigt er wieder die Meldung für eine abgebrochene Verarbeitung. Dieselbe Meldung zeigt er, wenn der Start scheitert, etwa mit 404, weil der Upload abgelaufen ist, oder mit 409, weil ein zweiter Tab schneller war. Bei jedem anderen Laufausgang bleibt alles wie heute.

Wer startet, bleibt dieselbe Person: Meldet sich jemand ab oder an, setzt der Wizard die Lieferung schon heute zurück. Ob die neue Verarbeitung lieferberechtigt ist, entscheidet ihr Start wie bei jeder anderen, für dieselbe Person und dasselbe Mandat.

**Abfrage.** Scheitert eine Abfrage mit einem Netzwerkfehler oder mit 502, 503 oder 504, fragt der Wizard nach 2 s erneut, danach jeweils mit doppeltem Abstand bis höchstens 30 s, insgesamt bis 15 Minuten nach dem ersten Fehler. Eine erfolgreiche Antwort setzt das zurück. Solange zeigt er den Hinweis «geopilot ist gerade nicht erreichbar. Der Stand der Verarbeitung wird weiter abgefragt.» Bricht der Wizard die Abfrage selbst ab, zählt das nicht. Die 15 Minuten decken den Start einer neuen Instanz samt Migrationen und ein kurzes Drain-Fenster; Abstand und Dauer sind Konstanten mit `shortcut:`.

**Texte.** Die beiden Hinweise in `public/locale` auf Deutsch, Englisch, Französisch und Italienisch, mit den Übersetzungen aus dem Glossar.

**Doku.**

- `GLOSSARY.md`: die Begriffe oben; der Eintrag Ausgang heisst Laufausgang, und die Links darauf in `docs/Ausfuehrungsprotokoll.md` und im Eintrag *Folgen* gehen mit.
- `docs/Ausfuehrungsprotokoll.md`: Das Protokoll hält neben dem Endzustand den Laufausgang fest (`RunOutcome`). Was aus der Verarbeitung wurde, leitet die Ansicht weiterhin beim Lesen ab.
- `docs/pipeline/pluginSystem.md` unter "Anforderungen an Pipeline-Prozessoren": Ein Prozessor kann für denselben Upload mehrmals laufen, und was er ausserhalb seines Arbeitsverzeichnisses tut, muss einen zweiten Lauf vertragen.
- CHANGELOG unter Added: Der Wizard startet eine Verarbeitung, die ein Neustart von geopilot abgebrochen hat, mit demselben Upload erneut, und seine Abfrage übersteht den Neustart.

## Prüfung

- Recorder: Am Ende des Laufs steht der Laufausgang im Protokoll, für jede Art von `RunOutcome`. Beendet das Herunterfahren eine Verarbeitung vor ihrem Lauf, steht dort `ShutDown`; unterbricht die Datenbank ihren Preflight, `Interrupted`; nach einem gescheiterten Preflight nichts.
- Statusabfrage: `runOutcome` kommt aus dem Protokoll, auch wenn die Instanz die Verarbeitung nicht im Speicher hat.
- Cypress mit Intercepts:
  - `cancelled` mit `shutDown`: Der Wizard startet mit derselben `uploadId` und demselben Mandat, fragt danach die neue Id ab und gibt mit ihr ab.
  - Nach zwei erneuten Starts, bei `timedOut` und bei `interrupted`: kein weiterer Start, sondern die Meldung für eine abgebrochene Verarbeitung.
  - Der erneute Start scheitert mit 404: dieselbe Meldung.
  - Zweimal 503 und ein Netzwerkfehler, dann 200: Der Wizard zeigt den Hinweis und fährt fort.
- Smoke-Test: Eine Verarbeitung mit einem langen Schritt starten, die Instanz sauber stoppen und wieder starten. Der Wizard startet die Verarbeitung erneut, und ihr Ergebnis lässt sich abgeben.
