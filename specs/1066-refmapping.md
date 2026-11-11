# Zuordnung der Referenzdaten im Admin-Portal pflegen (#1066)

Review: required. Zum ersten Mal reicht der Host Daten aus der Datenbank pro Job an einen Prozess weiter, ein neues Konzept in der Runtime. Sicherheitsrelevant ist es auch: Was ein Admin einträgt, holt ilivalidator ab, und davor schützt nur die Prüfung beim Speichern.

## Umsetzung

**Abweichungen.** Vom Issue: Die Umsetzung wartet nicht auf ADR 0018 (noch Draft) und #1067. Statt eines Interfaces in PipelineCore gibt es eines nur für diesen Fall, intern im Projekt `Geopilot.Pipeline` wie `IXtfDiffClient`; Plugins sehen es nicht. Die Bindung pro Job entsteht hier zum ersten Mal, #1067 kann sie übernehmen. Nennt ein Schritt selbst `refMapping` oder `refMappingFile`, gilt bei eingeschaltetem Schalter nicht seine Angabe, sondern der Schritt scheitert (siehe Validierung). Vom Design (Kommentar im Issue): Referenzen werden frei als `ilidata:<Id>` eingegeben statt aus einer Liste gewählt, und eine Referenz, die aus dem Index verschwunden ist, wird weder markiert, noch sperrt sie das Speichern. Beides bräuchte den Index, und der liegt mit den Referenzdaten beim ilitools-wrapper, an den geopilot nicht herankommt.

**Einträge.** Eine eigene Tabelle mit den Feldern des Modells `IliVRefData_V1_0`: `ignore` als «Aktiv», `topic`, `scope`, die Referenzen als Liste und `comment` als «Beschreibung». Eine API unter `api/v1/refmapping` (Policy `Admin`) liest, legt an, ändert und löscht und prüft beim Speichern, was das Issue aufzählt. Die Namen im Code folgen der Option `refMapping`.

**Schalter.** `RefMapping:Enabled` nach dem Muster von `MachineDelivery:Enabled`: aus in `appsettings.json`, an in `appsettings.Development.json` und für den Compose-Dienst `geopilot`, damit die CI die Operationen in die generierten Typen aufnimmt. Ist er aus, entfernt eine Convention den Controller, `CapabilitiesResponse` meldet ihn dem GUI, und der Host bindet nichts an Jobs.

**Bindung pro Job.** In `JobLifecycle.StartAsync` liest der Host bei eingeschaltetem Schalter alle Einträge in einer Abfrage, schreibt sie mit `RecordJobStartedAsync` ins Protokoll und bindet sie an die Pipeline des Jobs. `PipelineProcessFactory` gibt sie einem Konstruktor-Parameter vom Typ des internen Interfaces, wie heute die Clients des Wrappers, und die Startprüfung kennt den Typ. Das Interface liefert die Einträge als DTO, keine Datei: Die Api liest nur Daten, und das XTF entsteht dort, wo es verschickt wird. Die Bindung läuft über einen internen Weg neben `CreatePipeline`, die öffentliche Fläche des Pakets `Geopilot.Pipeline` bleibt, wie sie ist. Ohne Schalter bekommt der Prozess keine Bindung (`null`), mit Schalter eine, die auch leer sein kann.

**Validierung.** `XtfValidatorProcess` schreibt aus den Einträgen ein XTF, ein Objekt pro Eintrag, auch die inaktiven mit `ignore`, und schickt es über den bestehenden Kanal `refMappingFile`; der ilitools-wrapper bleibt unverändert. Bei leerer Liste geht keine Zuordnung mit. Bekommt der Prozess eine Bindung und nennt der Schritt zugleich `refMapping` oder `refMappingFile`, wirft sein Konstruktor, und der Schritt scheitert, weil sonst offen bliebe, welche Zuordnung gilt. Das hängt am Schalter, nicht an den Einträgen, damit ein Admin mit dem ersten Eintrag keine Pipeline bricht. Die Startprüfung konstruiert keine Prozesse, der Fehler zeigt sich darum erst bei der ersten Validierung. Mit Schalter bekommt jeder Validierungsschritt der Installation die Zuordnung aus dem Portal, abwählen kann ein Schritt sie nicht. Mit einer `toolVersion` vor 1.15.0 lehnt der Wrapper sie ab, und der Schritt scheitert.

**Protokoll.** Der Lauf bekommt eine Spalte `jsonb` für alles, was der Host beim Start an den Job bindet, nicht nur für die Zuordnung. Sie heisst nach diesem Konzept, etwa `Bindings`, und hält ein Objekt mit einem Schlüssel pro Bindung, hier `refMapping` mit den Einträgen, wie sie gebunden wurden. Die frühere Lieferung aus #1067 käme als weiterer Schlüssel dazu. Ohne Schalter fehlt der Schlüssel, ohne jede Bindung ist die Spalte `null`. Jeder Validierungsschritt des Jobs, der lief, bekam genau diese Einträge. Eine Installation hat höchstens einige hundert Einträge, ein Abbild pro Job ist darum vertretbar, wie das der Definition. Die Migrationen legen nur an, die Tabelle und diese Spalte; benennt #1195 die Tabellen des Protokolls vorher um, gilt der neue Name.

**GUI.** Eine Seite im Admin-Portal nach dem [Prototyp in Figma](https://www.figma.com/design/MbjIwJP67Ynt5GZtHKoBgH/geopilot?node-id=11783-3056&p=f), Liste und Detail wie bei den Mandaten (`AdminDetailForm`), im Menü nur mit Schalter. `scope` ist ein Textfeld, die Referenzen erfasst `FormChipInput`; eine neue Komponente braucht es nicht.

**Testwelt.** Der Seed legt die sieben Einträge aus `src/Geopilot.Api/Resources/dmav/refdata_mapping.xtf` an, und der DMAV-Schritt in `basicPipeline_02.yaml` nennt kein `refMappingFile` mehr, damit die lokale Testwelt über die Zuordnung aus dem Portal läuft. Die Datei entfällt, und was sie nennt (`TestData/Dmav/README.md`, `config/refdata-sources.yaml`), wird nachgeführt.

**Doku.** `docs/pipeline/Prozessoren/xtf-validierung.md` (Zuordnung aus dem Portal, Ausschluss von `refMapping` und `refMappingFile`, Verhalten ohne Eintrag), `docs/Referenzdaten.md` (Pflege im Portal, Schalter, Umstieg von der Datei), `docs/Ausfuehrungsprotokoll.md` (neue Spalte mit einer Beispielabfrage) und ein Eintrag im CHANGELOG unter Added.

## Nicht im Umfang

- Die Auswahl der Referenzdaten aus dem Index und die Anzeige ungültiger Referenzen (siehe Abweichungen), ebenso eine Suche nach Gemeindenamen für `scope`. Die Hinweise dazu im Issue entfallen, mit ihnen die Erweiterung der Content Security Policy.
- Import und Export als XTF und eine eigene Meldung für eine Referenz, die ilivalidator nicht auflösen kann.
- Eine Übernahme bestehender Dateien. Installationen, die heute `refMappingFile` nennen, laufen ohne Schalter unverändert weiter. Wer ihn einschaltet, erfasst die Einträge und entfernt `refMapping` und `refMappingFile` aus der Definition.
- Das Abbild im GUI des Protokolls und eine Route dafür; es steht in der Datenbank.

## Prüfung

- Unit-Tests: jede Regel der Prüfung beim Speichern; der Prozess, dessen Konstruktor mit Bindung und `refMapping` oder `refMappingFile` wirft, bei leerer Bindung nichts mitschickt und ohne Bindung wie heute läuft; die Bindung pro Job durch die Factory und die Startprüfung mit dem neuen Parametertyp.
- Ein Integrationstest gegen den ilitools-wrapper schickt das erzeugte XTF durch ilivalidator, wie das Issue verlangt.
- HTTP-Tests: Die Endpunkte verlangen Admin und fehlen ohne Schalter; `JwtSecurityTest` findet keinen neuen anonymen Endpunkt.
- Der Lauf hält das Abbild unter `refMapping` fest, ohne Schalter fehlt der Schlüssel.
- Cypress: Einträge anlegen, ändern, ausschalten und löschen, dazu die Fehlermeldung für eine Referenz ohne `ilidata:`.
- Smoke-Test in der DMAV-Testwelt: die Szenarien 1 und 2 aus dem Issue, sichtbar an den Referenzdaten im Log des Werkzeugs. Dazu zwei offene Fälle, deren Ergebnis `xtf-validierung.md` festhält: eine Lieferung, deren Scope keinen Eintrag hat (offen aus einem Kommentar im Issue), und eine Validierung ohne `scope` mit Zuordnung, wie sie jede andere Pipeline der Installation dann bekommt.
- CI ist grün, auch der Abgleich der generierten Typen.
