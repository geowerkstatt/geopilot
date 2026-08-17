# XTF Validierung

Validiert ein XTF-File anhand eines konfigurierbaren Profils. Die Validierung führt `ilivalidator` durch, aufgerufen über den [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper). Die Adresse des Wrappers gilt pro Umgebung und wird nicht am Prozessor, sondern zentral unter `Ilitools:IlitoolsWrapperAddress` konfiguriert.

## Implementierung

Ein XTF Validator Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess` definieren.

## Konfiguration

- `modelDirs`: Optionale Liste von INTERLIS-Modell-Repositories, aus denen die Modelle und das Validierungs-Profil aufgelöst werden. Erlaubt sind `http(s)`-URLs und der Platzhalter `%ITF_DIR` (das Verzeichnis der Transferdatei). Die Reihenfolge entscheidet: gesucht wird von links nach rechts, der erste Treffer gewinnt, ein früherer Eintrag verdeckt also gleichnamige Modelle eines späteren. Ist der Parameter gesetzt, ersetzt er die Voreinstellung des Werkzeugs vollständig, `https://models.interlis.ch/` muss dann also selbst aufgeführt werden. Weil es eine Liste ist, lässt sich der Parameter nur in einer Pipeline-Definition setzen, nicht in den Appsettings.
- `validationProfile`: Optionaler Parameter, welcher das Profil definiert, anhand dessen die Validierung durchgeführt wird. Der Wert ist die Dataset-Id, unter der das Profil in einem der `modelDirs` indexiert ist (`ilidata.xml`); das Präfix `ilidata:` darf angegeben werden, wird aber sonst ergänzt. Ohne Angabe validiert `ilivalidator` ohne Profil.
- `allObjectsAccessible`: Optionaler Parameter, standardmässig `true`. Sagt dem Validator, dass die geprüfte Datei alle Objekte enthält, die er braucht: ein Verweis auf ein Objekt ausserhalb ist dann ein Fehler und nicht eine übersprungene Prüfung. Der abgelöste interlis-check-service hat diese Einstellung über sein mitgeliefertes Profil auf jede Validierung angewendet, deshalb ist sie hier voreingestellt. Eine Pipeline, die Teile eines Datensatzes prüft und Verweise nach draussen zulassen muss, setzt den Wert auf `false`.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `iliFile`: Ein Input-File vom Typ `IPipelineFile`. Dieses File muss die XTF-Datei enthalten, welche validiert werden soll.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `ErrorLog`: Ein Output-File vom Typ `IPipelineFile?`, welches das Error-Log der Validierung enthält. Dieses Log enthält alle Fehler, welche bei der Validierung aufgetreten sind.
- `XtfLog`: Ein Output-File vom Typ `IPipelineFile?`, welches das XTF-Log der Validierung enthält. Dieses Log enthält alle Informationen über die Validierung, wie z.B. die Anzahl der validierten Objekte, die Anzahl der Fehler, etc.
- `StatusMessage`: Eine lokalisierte Status-Nachricht der Validierung vom Typ `LocalizedText`, welche über den Status der Validierung informiert.
- `ValidationSuccessful`: Wert vom Typ `bool`. Ist `true`, wenn das Input-File erfolgreich validiert werden konnte und valide ist. Ist `false`, wenn das Input-file nicht erfolgreich validiert werden konnte oder invalide ist.

Kann der Wrapper die Validierung gar nicht starten, etwa weil ein Eintrag in `modelDirs` abgelehnt wird, schlägt der Schritt mit einem Fehler fehl und liefert keine Logs.
