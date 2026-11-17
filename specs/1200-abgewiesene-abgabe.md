# Abgewiesene Abgabe im Web im Ausführungsprotokoll festhalten (#1200)

Review: waived. Additiv innerhalb des Konzepts aus #1189: Ein bestehendes Feld wird auch im Web gesetzt, ohne Migration und ohne Änderung an Antwort und Berechtigung von `POST api/v1/delivery`. Abzuwägen ist nur: Eine angemeldete Person, die die Job-ID eines lieferbaren, deklarierbaren Laufs kennt, kann mit einer abgewiesenen Abgabe dessen Grund setzen, einen festen Text.

Scheitert die Deklaration einer maschinellen Lieferung, steht der Grund am Lauf in `DeclarationFailure`, und die Ansicht der Verarbeitungen zeigt den Lauf als «gescheitert». Weist geopilot dagegen eine Abgabe im Web ab (`POST api/v1/delivery` nach «Lieferung erstellen»), erfährt das nur der Browser. Gibt die Person auf, zeigt die Ansicht «nicht abgegeben» wie bei einem Lauf, den niemand abgeben wollte, und ein Admin findet keinen Grund. Neu hält das Protokoll auch diese Abweisung fest.

## Umsetzung

**Wann.** `DeliveryController.Create` schreibt den Grund bei jedem Ausgang, der bei einem lieferbaren, deklarierbaren Lauf vorkommt:

- `JobNotFound`: Der Job ist abgelaufen (`Processing:JobRetention`) oder die Instanz neu gestartet.
- `MandateNotDeliverable`: Die Mitgliedschaft wurde entzogen, oder das Mandat nimmt keine Lieferungen mehr.
- `FieldRulesViolated`, `AssetRecordingFailed` und `NoAssets`.
- Eine Ausnahme aus `DeclareAsync`. Sie wird danach weitergeworfen, die Antwort bleibt dieselbe.

Nichts schreiben `AlreadyDeclared` (eine schon bestehende Lieferung), `JobNotDeliverable`, `JobNotDeclarable`, `JobWithoutMandate` (bei einem deklarierbaren Lauf unerreichbar) und ein Abbruch der Anfrage durch den Browser, denn der ist keine Abweisung.

**Text.** Derselbe wie bei der maschinellen Lieferung: `DescribeDeclarationFailure` und der Text des unerwarteten Fehlers ziehen aus `JobLifecycle` nach `DeliveryDeclarationResult`, beide Wege nehmen sie von dort. Feste Meldungen, bei abgewiesenen Lieferangaben Feld und Regel, nie ein Wert.

**Wie.** `RecordDeclarationFailedAsync` schreibt neu mit `ExecuteUpdateAsync` statt über den Change Tracker, wie `ReactionStore`. Im Web teilt der Recorder den `Context` des Requests, und nach einem Fehler beim Speichern hängen dort Lieferung und Reaktionen; ein `SaveChangesAsync` des Recorders würde sie mitspeichern. Die maschinelle Lieferung schreibt weiterhin aus einem eigenen Scope.

Das Update trifft nur einen Lauf mit `Declarability` `Declarable`, dessen `TerminalState` leer, `Success` oder `Warning` ist. Leer, weil die maschinelle Lieferung deklariert, bevor der Ausgang im Protokoll steht. Nötig ist die Bedingung für `JobNotFound`, denn dort hat niemand geprüft, ob der Lauf lieferbar und deklarierbar war. Sie entscheidet nur, ob das Protokoll schreibt, nicht die Antwort (ADR 0010). Geschrieben wird weich wie alle Einträge nach dem Start: Ein Fehler wird geloggt.

Bei mehreren Abweisungen überschreibt der letzte Grund. Gelingt ein späterer Versuch, bleibt er stehen, und die Ansicht zeigt die Lieferung, weil sie beim Ausgang vorgeht (`PipelineRunOutcomes.Determine`). Der Job im Speicher bleibt unberührt, ein Statusdokument gibt es im Web nicht.

**Doku.** `docs/Ausfuehrungsprotokoll.md`: `DeclarationFailure` gilt für beide Wege, bei der maschinellen Lieferung steht derselbe Text zusätzlich in `messages`. Leer heisst: deklariert, nie versucht oder an einer schon bestehenden Lieferung gescheitert. Unter *Abfrage* heisst «gescheitert» abgewiesen, im Web oder bei der maschinellen Lieferung, und «nicht abgegeben» nur noch nicht versucht. Ebenso die Doc-Kommentare, die beides auf die maschinelle Lieferung beschränken: `PipelineRun.DeclarationFailure`, `IPipelineRunRecorder.RecordDeclarationFailedAsync`, `PipelineRunResponse`, `PipelineRunOutcomeKind.DeclarationFailed` und `NotDelivered`. Die generierten Typen kommen neu aus `npm run openapi`.

Kein Hinweis auf frühere Läufe und kein CHANGELOG-Eintrag: `DeclarationFailure` erscheint wie die Ansicht erst mit 3.2, `release` hat beides nicht, und der Eintrag der Ansicht («why it led to no delivery») trifft weiterhin zu.

**Reihenfolge.** Begonnen nach dem Merge von #1188, der die Ansicht, die Ausgänge und den Abschnitt *Abfrage* bringt. Benennt #1195 vorher um, gelten die neuen Namen.

## Nicht im Umfang

- Wer die abgewiesene Abgabe versucht hat. Am Lauf steht weiterhin, wer ihn gestartet hat, eine Abweisung wegen fehlender Mitgliedschaft kann also von einer anderen Person stammen.
- Die Meldung im Web bleibt, wie sie ist. Dass sie meist allgemein ist (eine je Statuscode), ist ein eigenes Thema.
- Keine Übersetzung: Der Grund steht englisch da, wie bei der maschinellen Lieferung.
- Keine Änderung an der Ansicht: Sie zeigt «gescheitert», sobald `DeclarationFailure` an einem lieferbaren Lauf steht.

## Prüfung

- `DeliveryControllerTest`: Jeder der fünf Ausgänge und eine Ausnahme aus `DeclareAsync` schreiben den Text, den `JobLifecycle` für denselben Fall schreibt. `AlreadyDeclared`, `JobNotDeliverable` und `JobNotDeclarable` schreiben nichts. Die Antworten bleiben, wie sie waren.
- `PipelineRunRecorderTest`: Der Grund landet an einem deklarierbaren Lauf mit leerem, `Success`- oder `Warning`-Ausgang, nicht an einem nicht deklarierbaren oder unlieferbar beendeten. Ein zweiter Grund ersetzt den ersten. Eine ungespeicherte Lieferung im selben `Context` entsteht durch den Eintrag nicht.
- `JobLifecycleTest` bleibt unverändert grün.
- Von Auge im Dev-Stack: Verarbeitung starten, die Person vor «Lieferung erstellen» aus der Organisation des Mandats nehmen, abgeben. Die Ansicht zeigt den Lauf als «gescheitert» mit dem Grund. Mitgliedschaft zurückgeben, erneut abgeben: Die Ansicht zeigt die Lieferung.
- CI ist grün, auch der Abgleich der generierten Typen.
