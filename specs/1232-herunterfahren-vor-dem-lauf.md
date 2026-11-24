# Verarbeitungen im Preflight und in der Warteschlange enden beim Herunterfahren ohne Endzustand (#1232)

## Begriffe

- **Ausgang:** Den Ausgang Herunterfahren bekommt auch eine Verarbeitung, deren Lauf noch nicht begonnen hat: im Preflight, in seiner Warteschlange oder in der Warteschlange des Runners. Gemeldet wird er dann vom Preflight oder vom Runner, bevor ein Lauf beginnt.

## Umsetzung

Preflight und Runner melden jede Verarbeitung, die das Herunterfahren vor ihrem Lauf beendet, über `JobLifecycle.RunFinishedAsync` mit dem Ausgang Herunterfahren (`RunOutcome.ShutDown`). Damit gilt, was dieser Ausgang heute für eine laufende Verarbeitung bedeutet: `Decide` setzt sie auf `Cancelled` und gibt den Upload nicht frei, das Protokoll bekommt den Endzustand `Cancelled` mit dem Grund `host shutdown`, und die Pipeline wird entsorgt. Die Schritt-Zeilen bleiben `Pending`, denn keiner wurde erreicht. Neu ist nur der Übergang: Für diesen Ausgang setzt `ProcessingJobStore` auch eine Verarbeitung auf `Cancelled`, die noch `Pending` ist.

Der Preflight meldet so die Verarbeitung, deren Scan das Herunterfahren abbricht, und jede Anfrage, die er danach noch liest oder die noch in seiner Warteschlange liegt. Heute lässt er die erste unberührt, und für die nächste wirft `ProcessRequestAsync`, bevor er sie prüft: Sie ist gelesen, wird aber nie gemeldet.

Der Runner meldet die Einträge seiner Warteschlange gleich zu Beginn des Drain-Fensters, denn aus ihr startet darin nichts mehr. Laufende Verarbeitungen enden wie heute über `RunFinishedAsync`, wenn ihr Lauf endet oder das Fenster abläuft.

Jeder Dienst meldet seine Verarbeitungen parallel, sodass sie zusammen nicht länger brauchen als das bestehende Budget von 5 s für eine Schreibung ins Protokoll. Weil der Preflight vor dem Runner stoppt, geht sein Anteil von der Zeit ab, die der Host nach dem Drain-Fenster gewährt. Die Reserve in `ShutdownDrainExtensions` steigt deshalb von 10 s auf 15 s. Reicht auch sie bei einer langsamen Datenbank nicht, bleiben die übrigen Verarbeitungen ohne Endzustand, wie heute.

Die Statusantwort ändert sich nicht: Während des Herunterfahrens antwortet die Instanz nicht mehr, und danach kennt sie die Verarbeitung nicht.

Doku in `docs/Ausfuehrungsprotokoll.md`:

- Ohne Terminal-Status bleibt eine Verarbeitung nach einem harten Abbruch, oder wenn das Herunterfahren ihren Endzustand nicht mehr schreiben konnte.
- Unter "Wo ist der Preflight?": `Cancelled` mit `host shutdown` und alle Schritte `Pending` heisst, dass das Herunterfahren die Verarbeitung vor ihrem ersten Schritt beendet hat, im Preflight oder in der Warteschlange. "Durch einen Neustart im Preflight unterbrochen" gilt nur noch für einen harten Abbruch.
- Die Abfrage für Restart-Opfer zählt auch `Cancelled` mit `host shutdown`.

CHANGELOG unter Changed: Beim Herunterfahren enden auch Verarbeitungen, die im Preflight sind oder auf einen freien Platz warten, als abgebrochen und behalten ihren Upload.

## Prüfung

- `PreflightBackgroundServiceTest`: Ein Scan, den das Herunterfahren abbricht, endet als `Cancelled` mit `host shutdown`, ohne dass der Upload freigegeben wird.
- Über `StartAsync` und `StopAsync` des Preflight-Dienstes: Eine Anfrage hinter einem laufenden Scan endet ebenso, auch die, die der Dienst nach dem Abbruch noch liest.
- `ProcessingRunnerTest`: Bei `MaxConcurrentJobs` 1 wartet eine zweite Verarbeitung. Sie steht schon zu Beginn des Drain-Fensters auf `Cancelled` mit `host shutdown`, und ihr Upload bleibt.
- `HostedServiceShutdownTest`: Der Shutdown-Timeout deckt das Drain-Fenster plus 15 s.
- Smoke-Test: Mit `MaxConcurrentJobs` 1 und eingeschaltetem Virenscan drei Verarbeitungen starten: die erste mit einem langen Schritt, die zweite wartet, die dritte mit einer grossen Datei, sodass sie im Preflight steckt. Dann die Instanz sauber stoppen. Im Protokoll stehen alle drei auf `Cancelled` mit `host shutdown`, und ihre Uploads liegen noch da.
