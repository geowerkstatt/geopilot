# GPKG Daten-Aktualisierung

Aktualisiert die Daten einer bestehenden GeoPackage-Datei mit INTERLIS-Transferdateien (`.xtf` oder `.itf`). Die Aktualisierung führt `ili2gpkg --update` aus, aufgerufen über den [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper), dessen Adresse zentral unter `Ilitools:IlitoolsWrapperAddress` konfiguriert wird. Was die an `ili2gpkg` übergebenen Optionen bewirken, beschreibt die [ili2db-Dokumentation](https://github.com/claeis/ili2db/blob/master/docs/ili2db.rst#referenz).

## Implementierung

Ein GPKG Aktualisierungs-Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Gpkg.Ili2GpkgUpdateProcess` definieren.

## Konfiguration

- `modelRepository`: Optionaler Parameter, welcher den Pfad eines ZIP-Archivs im Ressourcen-Verzeichnis nennt (`Storage:ResourcesDirectory`), also einer Datei, die das Deployment mitbringt. Das Archiv enthält ein vollständiges INTERLIS-Repository (`ilimodels.xml`, `ilidata.xml`, optional `ilisite.xml` und Profile) und wird vom Wrapper in den Unterordner `repository/` des Session-Verzeichnisses entpackt. Damit sein Inhalt gefunden wird, muss `modelDirs` den Eintrag `%XTF_DIR/repository` enthalten. So lässt sich ein Repository verwenden, das nicht publiziert ist. Der Inhalt gilt als Konfiguration und wird unverändert verwendet: er kann Modelle definieren, ein Profil mitbringen und über eine `ilisite.xml` auf weitere Repositories verweisen. Fehlt die Datei oder liegt sie ausserhalb des Ressourcen-Verzeichnisses, startet die Applikation nicht.
- `modelDirs`: Optionale INTERLIS-Modell-Repositories, aus denen die Modelle und die Meta-Konfiguration (`metaConfig`) aufgelöst werden, als **ein Wert mit Semikolon getrennt** (`%ILI_FROM_DB;https://models.interlis.ch/;%XTF_DIR/repository`), also in derselben Schreibweise wie die Option `--modeldir` des Werkzeugs. Erlaubt sind `http(s)`-URLs und die Platzhalter `%ILI_FROM_DB` sowie `%XTF_DIR` (das Session-Verzeichnis des Wrappers), allein oder mit relativem Unterordner: Der Wrapper legt das entpackte `modelRepository`-Archiv in `%XTF_DIR/repository` ab. Die Reihenfolge entscheidet: gesucht wird von links nach rechts, der erste Treffer gewinnt, ein früherer Eintrag verdeckt also gleichnamige Modelle eines späteren. Ist der Parameter gesetzt, ersetzt er die Voreinstellung des Werkzeugs vollständig.
- `toolVersion`: Optionale Version von `ili2gpkg`, die dieser Schritt ausführt. Welche Versionen es gibt, bestimmt das Deployment des [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper): er bietet an, was sein Image mitbringt, und lehnt eine Version, die er nicht anbietet, ab, bevor das Werkzeug startet. Ohne Angabe läuft die Voreinstellung des Deployments, die bewusst nicht die neuste angebotene Version sein muss: so lässt sich eine neue Version anbieten, ohne dass sie von selbst greift. Welche Version tatsächlich lief, steht im Kopf des Text-Logs (`ili2gpkg-5.5.2-...`), eine Lieferung trägt den Nachweis also mit.
- `pluginIds`: Optionale Liste der Plugin-Ids, deren benutzerdefinierte Funktionen ein Modell in seinen Constraints aufrufen darf, als **ein Wert mit Semikolon getrennt** (`geow-interlis-functions;ngk-so`). Welche Ids es gibt, bestimmt das Deployment des [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper): er bietet an, was in seinem Plugin-Verzeichnis liegt, und lehnt eine unbekannte Id ab, bevor das Werkzeug startet. Ohne Angabe läuft kein Plugin, und mit `disableValidation` wirkt keines, weil dann keine Constraints geprüft werden. Weil ein Plugin Code ist, der bestimmt, was eine Prüfung bedeutet, gehört der Parameter üblicherweise in die Basis-Konfiguration der Appsettings: dort kann ihn keine Pipeline-Definition und kein einzelner Schritt überschreiben. Die Schreibweise als ein Skalar ist genau das, was ihn dort verwendbar macht. Ruft ein Constraint eine Funktion auf, für die kein Plugin geladen ist, überspringt `ili2gpkg` den Constraint mit einer Warnung und meldet den Lauf trotzdem als erfolgreich. Der Prozessor lässt das nicht durchgehen: er erkennt den übersprungenen Constraint im Log und bricht den Schritt mit einem Fehler ab, weil das Resultat über diese Prüfung nichts aussagt.
- `metaConfig`: Optionale Meta-Konfiguration als Dataset-Id in einem der `modelDirs`; das Präfix `ilidata:` wird ergänzt (`--metaConfig`). Wird sie in keinem der `modelDirs` gefunden, schlägt der Schritt mit einem Fehler fehl, statt `Success` auf `false` zu setzen: ein Fehler in der Pipeline-Definition wird so nicht als Urteil über die gelieferten Daten dargestellt.
- Optionen vom Typ `bool`, alle optional und standardmässig `false`, jede als gleichnamige Option an `ili2gpkg`: `disableValidation`, `skipGeometryErrors`, `skipReferenceErrors`, `strokeArcs`.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `transferFiles`: Input vom Typ `IPipelineFile[]` mit mindestens einer Transferdatei. Alle Dateien müssen dasselbe Format haben (`.xtf`/`.xml` oder `.itf`).
- `dbFile`: Die zu aktualisierende GeoPackage-Datei vom Typ `IPipelineFile`, Pflicht.
- `models`: Optionale Modellnamen vom Typ `string[]`; ein Eintrag darf auch mehrere Namen mit Semikolon getrennt enthalten (`--models`).
- `dataset`: Optionale Datensatznamen vom Typ `string[]`; ein Eintrag darf auch mehrere Namen mit Semikolon getrennt enthalten (`--dataset`).
- `defaultSrsCode`: Optionaler EPSG-Code vom Typ `int` (`--defaultSrsCode`).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert.

- `Success`: Wert vom Typ `bool`, `true` wenn die Aktualisierung erfolgreich war.
- `DbFile`: Die aktualisierte GeoPackage-Datei als `IPipelineFile[]` mit einem Eintrag, bei einem Fehler leer. Die übergebene `dbFile` bleibt unverändert.
- `ErrorLog`, `XtfLog`: Text- und XTF-Log der Aktualisierung vom Typ `IPipelineFile`.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`.
