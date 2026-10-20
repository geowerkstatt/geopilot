# GPKG Validierung

Validiert die Daten einer GeoPackage-Datei gegen ihre INTERLIS-Modelle. Die Validierung führt `ili2gpkg --validate` aus, aufgerufen über den [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper), dessen Adresse zentral unter `Ilitools:IlitoolsWrapperAddress` konfiguriert wird. Was die an `ili2gpkg` übergebenen Optionen bewirken, beschreibt die [ili2db-Dokumentation](https://github.com/claeis/ili2db/blob/master/docs/ili2db.rst#referenz).

## Implementierung

Ein GPKG Validator Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Gpkg.Ili2GpkgValidatorProcess` definieren.

## Konfiguration

- `modelRepository`: Optionaler Parameter, welcher den Pfad eines ZIP-Archivs im Ressourcen-Verzeichnis nennt (`Storage:ResourcesDirectory`), also einer Datei, die das Deployment mitbringt. Das Archiv enthält ein vollständiges INTERLIS-Repository (`ilimodels.xml`, `ilidata.xml`, optional `ilisite.xml` und Profile) und wird vom Wrapper in den Unterordner `repository/` des Session-Verzeichnisses entpackt. Damit sein Inhalt gefunden wird, muss `modelDirs` den Eintrag `%XTF_DIR/repository` enthalten. So lässt sich ein Repository verwenden, das nicht publiziert ist. Der Inhalt gilt als Konfiguration und wird unverändert verwendet: er kann Modelle definieren, ein Profil mitbringen und über eine `ilisite.xml` auf weitere Repositories verweisen. Fehlt die Datei oder liegt sie ausserhalb des Ressourcen-Verzeichnisses, startet die Applikation nicht.
- `modelDirs`: Optionale INTERLIS-Modell-Repositories, aus denen die Modelle und das Validierungs-Profil (`validConfig`) aufgelöst werden, als **ein Wert mit Semikolon getrennt** (`%ILI_FROM_DB;https://models.interlis.ch/;%XTF_DIR/repository`), also in derselben Schreibweise wie die Option `--modeldir` des Werkzeugs. Erlaubt sind `http(s)`-URLs und die Platzhalter `%ILI_FROM_DB` sowie `%XTF_DIR` (das Session-Verzeichnis des Wrappers), allein oder mit relativem Unterordner: Der Wrapper legt das entpackte `modelRepository`-Archiv in `%XTF_DIR/repository` ab. Die Reihenfolge entscheidet: gesucht wird von links nach rechts, der erste Treffer gewinnt, ein früherer Eintrag verdeckt also gleichnamige Modelle eines späteren. Ist der Parameter gesetzt, ersetzt er die Voreinstellung des Werkzeugs vollständig.
- `toolVersion`: Optionale Version von `ili2gpkg`, die dieser Schritt ausführt. Welche Versionen es gibt, bestimmt das Deployment des [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper): er bietet an, was sein Image mitbringt, und lehnt eine Version, die er nicht anbietet, ab, bevor das Werkzeug startet. Ohne Angabe läuft die Voreinstellung des Deployments, die bewusst nicht die neuste angebotene Version sein muss: so lässt sich eine neue Version anbieten, ohne dass sie von selbst greift. Welche Version tatsächlich lief, steht im Kopf des Text-Logs (`ili2gpkg-5.5.2-...`), eine Lieferung trägt den Nachweis also mit.
- `pluginIds`: Optionale Liste der Plugin-Ids, deren benutzerdefinierte Funktionen ein Modell in seinen Constraints aufrufen darf, als **ein Wert mit Semikolon getrennt** (`geow-interlis-functions;ngk-so`). Welche Ids es gibt, bestimmt das Deployment des [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper): er bietet an, was in seinem Plugin-Verzeichnis liegt, und lehnt eine unbekannte Id ab, bevor das Werkzeug startet. Ohne Angabe läuft kein Plugin. Weil ein Plugin Code ist, der bestimmt, was eine Prüfung bedeutet, gehört der Parameter üblicherweise in die Basis-Konfiguration der Appsettings: dort kann ihn keine Pipeline-Definition und kein einzelner Schritt überschreiben. Die Schreibweise als ein Skalar ist genau das, was ihn dort verwendbar macht. Ruft ein Constraint eine Funktion auf, für die kein Plugin geladen ist, überspringt `ili2gpkg` den Constraint mit einer Warnung und meldet den Lauf trotzdem als erfolgreich. Der Prozessor lässt das nicht durchgehen: er erkennt den übersprungenen Constraint im Log und bricht den Schritt mit einem Fehler ab, weil das Resultat über diese Prüfung nichts aussagt.
- `validConfig`: Optionales Validierungs-Profil als Dataset-Id in einem der `modelDirs`; das Präfix `ilidata:` wird ergänzt (`--validConfig`). Wird es in keinem der `modelDirs` gefunden, schlägt der Schritt mit einem Fehler fehl, statt `ValidationSuccessful` auf `false` zu setzen: ein Fehler in der Pipeline-Definition wird so nicht als Urteil über die Daten dargestellt.
- `disableAreaValidation`, `forceTypeValidation`: Optionale `bool`, standardmässig `false`, je als gleichnamige Option an `ili2gpkg`.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `dbFile`: Die zu validierende GeoPackage-Datei vom Typ `IPipelineFile`, Pflicht.
- `models`, `topics`, `baskets`: Optionale Werte vom Typ `string`, je mit Semikolon getrennt. Sie grenzen ein, was validiert wird.
- `dataset`: Optionaler Datensatzname vom Typ `string` (`--dataset`).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert.

- `ValidationSuccessful`: Wert vom Typ `bool`, `true` wenn die Daten valide sind.
- `ErrorLog`, `XtfLog`: Text- und XTF-Log der Validierung vom Typ `IPipelineFile?`. Das `XtfLog` lässt sich wie bei der XTF Validierung an die [XTF Fehlervisualisierung](xtf-fehlervisualisierung.md) weitergeben.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`.
