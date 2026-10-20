# GPKG Schema-Import

Legt aus INTERLIS-Modellen das Schema einer GeoPackage-Datei an, ohne Daten zu importieren. Den Import führt `ili2gpkg` aus, aufgerufen über den [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper), dessen Adresse zentral unter `Ilitools:IlitoolsWrapperAddress` konfiguriert wird. Was die an `ili2gpkg` übergebenen Optionen bewirken, beschreibt die [ili2db-Dokumentation](https://github.com/claeis/ili2db/blob/master/docs/ili2db.rst#referenz).

## Implementierung

Ein GPKG Schema-Import Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.Gpkg.Ili2GpkgSchemaImportProcess` definieren.

## Konfiguration

- `modelRepository`: Optionaler Parameter, welcher den Pfad eines ZIP-Archivs im Ressourcen-Verzeichnis nennt (`Storage:ResourcesDirectory`), also einer Datei, die das Deployment mitbringt. Das Archiv enthält ein vollständiges INTERLIS-Repository (`ilimodels.xml`, `ilidata.xml`, optional `ilisite.xml` und Profile) und wird vom Wrapper in den Unterordner `repository/` des Session-Verzeichnisses entpackt. Damit sein Inhalt gefunden wird, muss `modelDirs` den Eintrag `%XTF_DIR/repository` enthalten. So lässt sich ein Repository verwenden, das nicht publiziert ist. Der Inhalt gilt als Konfiguration und wird unverändert verwendet: er kann Modelle definieren, ein Profil mitbringen und über eine `ilisite.xml` auf weitere Repositories verweisen. Fehlt die Datei oder liegt sie ausserhalb des Ressourcen-Verzeichnisses, startet die Applikation nicht.
- `modelDirs`: Optionale INTERLIS-Modell-Repositories, aus denen die Modelle aufgelöst werden, als **ein Wert mit Semikolon getrennt** (`%ILI_FROM_DB;https://models.interlis.ch/;%XTF_DIR/repository`), also in derselben Schreibweise wie die Option `--modeldir` des Werkzeugs. Erlaubt sind `http(s)`-URLs und die Platzhalter `%ILI_FROM_DB` sowie `%XTF_DIR` (das Session-Verzeichnis des Wrappers), allein oder mit relativem Unterordner: Der Wrapper legt das entpackte `modelRepository`-Archiv in `%XTF_DIR/repository` ab. Die Reihenfolge entscheidet: gesucht wird von links nach rechts, der erste Treffer gewinnt, ein früherer Eintrag verdeckt also gleichnamige Modelle eines späteren. Ist der Parameter gesetzt, ersetzt er die Voreinstellung des Werkzeugs vollständig.
- `toolVersion`: Optionale Version von `ili2gpkg`, die dieser Schritt ausführt. Welche Versionen es gibt, bestimmt das Deployment des [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper): er bietet an, was sein Image mitbringt, und lehnt eine Version, die er nicht anbietet, ab, bevor das Werkzeug startet. Ohne Angabe läuft die Voreinstellung des Deployments, die bewusst nicht die neuste angebotene Version sein muss: so lässt sich eine neue Version anbieten, ohne dass sie von selbst greift. Welche Version tatsächlich lief, steht im Kopf jedes Text-Logs (`ili2gpkg-5.5.2-...`), eine Lieferung trägt den Nachweis also mit.
- Schema-Optionen vom Typ `bool`, alle optional und standardmässig `false`. Jede geht als gleichnamige Option an `ili2gpkg` (z.B. `createBasketCol` als `--createBasketCol`): `createBasketCol`, `createFk`, `createGeomIdx`, `createEnumTabs`, `createTidCol`, `beautifyEnumDispName`, `smart1Inheritance`, `smart2Inheritance`, `strokeArcs`, `sqlEnableNull`, `sqlColsAsText`.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `modelFiles`: Input vom Typ `IPipelineFile[]` mit mindestens einer `*.ili`-Datei. Mehrere Modelldateien werden nacheinander in dieselbe GeoPackage-Datei importiert; beim erste fehlgeschlagene Import wird der Vorgang abgebrochen und keine weitere Modelldatei mehr importiert.
- `dbFile`: Optionale bestehende GeoPackage-Datei vom Typ `IPipelineFile`, in die das Schema ergänzt wird. Ohne Angabe entsteht eine neue Datei.
- `models`: Optionale Modellnamen vom Typ `string`, mit Semikolon getrennt (`--models`).
- `defaultSrsCode`: Optionaler EPSG-Code vom Typ `int` (`--defaultSrsCode`).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert.

- `Success`: Wert vom Typ `bool`, `true` wenn alle Importe erfolgreich waren.
- `DbFile`: Die erzeugte GeoPackage-Datei als `IPipelineFile[]` mit einem Eintrag, bei einem Fehler leer.
- `ErrorLogs`, `XtfLogs`: Text- und XTF-Logs vom Typ `IPipelineFile[]`, ein Eintrag pro importierter Modelldatei.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`.
