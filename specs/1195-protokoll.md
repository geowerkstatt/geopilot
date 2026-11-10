# Ausführungsprotokoll: «Protokoll» und `Recording` statt «Lauf», «Verarbeitung» und `PipelineRun` (#1195)

Review: required. Die Migration benennt Tabellen mit bestehenden Protokollen um, und Tabellen, Spalten und Route, auf die Betreiber eigene Abfragen schreiben, heissen danach anders.

## Umsetzung

**Migration.** Sie entsteht nach dem Merge von #1188 und ist die jüngste: Was 3.2 gegenüber 3.1 an Migrationen mitbringt, legt Tabellen mit Bezug auf `PipelineRuns` an und läuft beim Upgrade vor ihr. Kommt danach auf `release` noch eine Migration am Protokoll dazu, muss sie beim Rückführen nach `main` die neuen Namen verwenden.

Von Hand und nur mit Umbenennungen, nie Drop und Create, damit die Daten bleiben. Auch Schlüssel werden umbenannt statt neu angelegt (`ALTER TABLE … RENAME CONSTRAINT` über `migrationBuilder.Sql`, Indizes mit `RenameIndex`). Beim Start laufen alle ausstehenden Migrationen als ein Skript mit dem Standard-Timeout von 30 Sekunden (`ContextExtensions.ApplyMigrations`), und der Neuaufbau von Primärschlüsseln auf grossen Protokolltabellen könnte ihn reissen; Umbenennen ändert nur den Katalog. `20260826110142_PluralizeProtocolChildTables` zeigt die Namenskonvention von EF, sein Entfernen und Neuanlegen der Schlüssel übernehmen wir nicht.

- Tabellen: `PipelineRuns` wird `Recordings`, aus `PipelineRunFiles`, `PipelineRunSteps`, `PipelineRunInputs`, `PipelineRunConditions`, `PipelineRunArtifacts` und `PipelineRunReactionEvents` werden `RecordingFiles`, `RecordingSteps`, `RecordingInputs`, `RecordingConditions`, `RecordingArtifacts` und `RecordingReactionEvents`.
- Spalten: `PipelineRunId` wird `RecordingId`, `PipelineRunStepId` wird `RecordingStepId`. `PipelineId` bleibt, die Spalte meint die Pipeline.
- Alle Primär- und Fremdschlüssel folgen der Konvention, auch die zu anderen Tabellen (`FK_PipelineRuns_Mandates_MandateId`, `FK_PipelineRuns_Users_UserId`, `FK_PipelineRuns_MachineClients_MachineClientId`, `FK_PipelineRunReactionEvents_Users_UserId`), dazu alle Indizes, auch `IX_PipelineRuns_ClientReference` und `IX_PipelineRuns_StartedAt_Id` aus #1188, und `CK_PipelineRuns_Declarer`.
- Die Sequenzen der Identity-Spalten behalten ihren Namen. PostgreSQL benennt sie nicht mit, und nichts adressiert sie.
- `Down` benennt alles zurück.

**Code.** Alles, was die Wurzel des Protokolls meint, heisst `Recording…` oder im Text «recording»: die Modelle, die `DbSet`s und `RecordingsWithIncludes` im `Context`, die Verträge `Recording…Response`, der `RecordingController` mit der Route `api/v1/recording`, die `PipelineRun…`-Typen aus #1188, die Tests samt Dateinamen, Variablen und Parameter, Kommentare und XML-Doku (sie landet in den generierten Typen), im Frontend die Route der API-Aufrufe sowie Intercepts und Titel der Cypress-Specs, soweit #1188 sie nicht schon umbenannt hat. Ein Intercept auf die alte Route scheitert nicht, er greift nur nicht mehr. Zu den umbenannten Typen gehört `PipelineRunOutcome` aus #1188: Es umfasst auch Lieferung und Weitergabe, ist also mehr als das Ende der Pipeline und nicht mit `RunOutcome` zu verwechseln. `IPipelineRunRecorder` und `PipelineRunRecorder` werden `IRecorder` und `Recorder`, weil `RecordingRecorder` doppelt sagt, was gemeint ist. Was bleibt, nennt die Abgrenzung im Issue. Die generierten Typen kommen neu aus `npm run openapi`.

**Doku.** `docs/Ausfuehrungsprotokoll.md` mit Tabellen, Route und SQL-Beispielen, `docs/Reaktionen.md` und der Doc-Kommentar in `ProcessingOptions.cs` nennen die neuen Namen. Dort und in `CONTEXT.md` wird «Lauf» zu «Protokoll», wo die Wurzel des Protokolls gemeint ist, und `CONTEXT.md` nennt `Recording` als Namen im Code. Wo das Ende der Pipeline gemeint ist (Ausgang), bleibt es beim Lauf der Pipeline.

**CHANGELOG.** Der Eintrag zum Protokoll, den auch `release` trägt, behält die alte Route, denn so liefert 3.1 sie aus. Der Eintrag zu den Reaktionen, den nur `main` hat, nennt die neue. Unter Changed kommt ein Eintrag für Betreiber: Tabellen, Spalten und Route des Protokolls heissen neu, eigene Abfragen und Skripte sind anzupassen.

## Nicht im Umfang

Es gilt die Abgrenzung im Issue. Dazu: keine Weiterleitung der alten Route und keine Umbenennung auf `release`.

## Prüfung

- `dotnet tool run dotnet-ef migrations has-pending-model-changes` meldet nichts.
- Datenerhalt: Auf einer Datenbank im Stand von `main` mit #1188 ein paar Jobs laufen lassen, mindestens einen bis zur Lieferung, die Zeilen pro Tabelle zählen, migrieren, unter den neuen Namen dieselben Zahlen. Ein alter Job ist unter `GET api/v1/recording/{jobId}` vollständig abrufbar. `Down` und wieder `Up` einmal durchspielen.
- Upgrade: Eine Datenbank mit Protokollen im Stand des Branches `release` migriert ohne Fehler auf den neuen Stand.
- In der migrierten Datenbank liefern `pg_constraint` und `pg_indexes` keinen Namen mit `PipelineRun` mehr.
- `git grep -i pipelinerun` und `git grep -i "pipeline run"` treffen nur noch die Ausnahmen aus dem Issue, bestehende Migrationen und Stellen, die den Lauf der Pipeline selbst meinen.
- Die SQL-Beispiele aus `docs/Ausfuehrungsprotokoll.md` laufen gegen die migrierte Datenbank.
- CI ist grün, auch der Abgleich der generierten Typen.
