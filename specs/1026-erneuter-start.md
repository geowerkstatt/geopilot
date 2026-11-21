# Eine Verarbeitung, die das Herunterfahren abbricht, startet im Wizard erneut (#1026)

## Begriffe

- **Erneuter Start** (neu): Der Wizard startet für denselben Upload und dasselbe Mandat eine neue Verarbeitung, nachdem das Herunterfahren die vorige abgebrochen hat. Die neue hat ihre eigene Id und ihr eigenes Protokoll, die abgebrochene bleibt, wie sie ist. Im Code: im Wizard (`deliveryProvider.tsx`). Vermeiden: Wiederaufnahme (die setzte den Lauf fort), Wiedereinreichung, Neustart (so heisst der von geopilot).
  _Übersetzungen_: EN new start, FR un nouveau lancement, IT un nuovo avvio
- **Ausgang:** steht neu im Protokoll und in der Statusantwort, als Spalte `RunOutcome` und als Feld `runOutcome`. Nicht zu verwechseln mit `outcome` der Ansicht eines Protokolls, das sagt, was aus der Verarbeitung wurde.

## Umsetzung

**Ausgang im Protokoll.** `Recordings` bekommt die Spalte `RunOutcome`, ein Enum mit einem Wert je Art von `RunOutcome`, gespeichert als Text wie die übrigen Enums. Geschrieben wird sie mit dem Endzustand in `RunFinishedAsync`, also auch, wenn das Herunterfahren eine Verarbeitung vor ihrem Lauf beendet. Leer bleibt sie, solange die Verarbeitung läuft, und wenn ihr Preflight scheiterte. Die Migration fügt nur die Spalte hinzu; ältere Zeilen bleiben leer, denn der Wizard braucht den Ausgang nur für frische Verarbeitungen. `FailureReason` bleibt als Text für Menschen.

**Statusantwort.** `ProcessingJobResponse` bekommt `runOutcome` aus dieser Spalte, `null` ohne Ausgang, und Swagger beschreibt das Feld. Die Statusabfrage liest es wie alles andere aus dem Protokoll, also auch nach einem Neustart von geopilot.

**Erneuter Start.** Endet die Abfrage in `deliveryProvider.tsx` mit `cancelled` und `runOutcome` `shutDown`, startet der Wizard mit `uploadId` und dem gewählten Mandat über `POST api/v2/processing` eine neue Verarbeitung, statt die Meldung für eine abgebrochene zu zeigen. Er bleibt auf dem Schritt der Verarbeitung, übernimmt die neue Id für Abfrage, Downloads und Abgabe und zeigt den Hinweis, dass er die Verarbeitung nach einem Neustart von geopilot erneut gestartet hat.

Nach zwei erneuten Starts zeigt er wieder die Meldung für eine abgebrochene Verarbeitung. Dieselbe Meldung zeigt er, wenn der Start scheitert, etwa mit 404, weil der Upload abgelaufen ist, oder mit 409, weil ein zweiter Tab schneller war. Bei jedem anderen Ausgang bleibt alles wie heute, auch bei einer Zeitüberschreitung.

Wer startet, bleibt dieselbe Person: Meldet sich jemand ab oder an, setzt der Wizard die Lieferung schon heute zurück. Ob die neue Verarbeitung lieferberechtigt ist, entscheidet ihr Start wie bei jeder anderen, für dieselbe Person und dasselbe Mandat.

**Abfrage.** Scheitert eine Abfrage mit einem Netzwerkfehler oder mit 502, 503 oder 504, fragt der Wizard nach 2 s erneut, danach jeweils mit doppeltem Abstand bis höchstens 30 s, insgesamt bis 15 Minuten nach dem ersten Fehler. Eine erfolgreiche Antwort setzt das zurück. Solange zeigt er den Hinweis, dass geopilot gerade nicht erreichbar ist und die Abfrage weiterläuft. Bricht der Wizard die Abfrage selbst ab, zählt das nicht. Die 15 Minuten decken den Start einer neuen Instanz samt Migrationen und ein kurzes Drain-Fenster; Abstand und Dauer sind Konstanten mit `shortcut:`.

**Texte.** Die beiden Hinweise in `public/locale` auf Deutsch, Englisch, Französisch und Italienisch, mit den Übersetzungen aus dem Glossar.

**Doku.**

- `docs/Ausfuehrungsprotokoll.md`: Das Protokoll hält neben dem Endzustand den Ausgang fest (`RunOutcome`). Was aus der Verarbeitung wurde, leitet die Ansicht weiterhin beim Lesen ab.
- `docs/pipeline/pluginSystem.md` unter "Anforderungen an Pipeline-Prozessoren": Ein Prozessor kann für denselben Upload mehrmals laufen, und was er ausserhalb seines Arbeitsverzeichnisses tut, muss einen zweiten Lauf vertragen.
- CHANGELOG unter Added: Der Wizard startet eine Verarbeitung, die ein Neustart von geopilot abgebrochen hat, mit demselben Upload erneut, und seine Abfrage übersteht den Neustart.

## Prüfung

- Recorder: Am Ende des Laufs steht der Ausgang im Protokoll, für jede Art von `RunOutcome`. Beendet das Herunterfahren eine Verarbeitung vor ihrem Lauf, steht dort `ShutDown`, nach einem gescheiterten Preflight nichts.
- Statusabfrage: `runOutcome` kommt aus dem Protokoll, auch wenn die Instanz die Verarbeitung nicht im Speicher hat.
- Cypress mit Intercepts:
  - `cancelled` mit `shutDown`: Der Wizard startet mit derselben `uploadId` und demselben Mandat, fragt danach die neue Id ab und gibt mit ihr ab.
  - Nach zwei erneuten Starts und bei `timedOut`: kein weiterer Start, sondern die Meldung für eine abgebrochene Verarbeitung.
  - Der erneute Start scheitert mit 404: dieselbe Meldung.
  - Zweimal 503 und ein Netzwerkfehler, dann 200: Der Wizard zeigt den Hinweis und fährt fort.
- Smoke-Test: Eine Verarbeitung mit einem langen Schritt starten, die Instanz sauber stoppen und wieder starten. Der Wizard startet die Verarbeitung erneut, und ihr Ergebnis lässt sich abgeben.
