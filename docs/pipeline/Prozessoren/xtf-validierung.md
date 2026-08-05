# XTF Validierung

Validiert ein XTF-File anhand eines konfigurierbaren Profils. Dabei wird IliCop als REST-Service verwendet, um die Validierung durchzuführen.

Die Basis-URL für den IliCop-Service wird über den Pflichtparameter `checkServiceBaseUrl` definiert (siehe [Konfiguration eines Pipeline-Prozessors](../Pipelines.md#konfiguration-eines-pipeline-prozessors)). Da diese URL pro Umgebung gilt, wird sie üblicherweise als nicht überschreibbare Basis-Konfiguration in den Appsettings unter `Pipeline:ProcessConfigs` für diesen Prozessor hinterlegt.

## Implementierung

Ein XTF Validator Prozess muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess` definieren.

## Konfiguration

- `checkServiceBaseUrl`: Pflichtparameter, welcher die Basis URL des IliCop-Services definiert, welcher für die Validierung verwendet wird.
- `validationProfile`: Optionaler Parameter, welcher das Profil definiert, anhand dessen die Validierung durchgeführt wird. Die möglichen Werte müssen in der Dokumentation von IliCop nachgeschlagen werden.
- `pollInterval`: Optionaler Parameter, welcher das Intervall definiert, in welchem der Prozess den Status der Validierung abfragt. Der Wert wird in Millisekunden angegeben. Der Standardwert ist zwei Sekunden.

## Input

Der Name des Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `iliFile`: Ein Input-File vom Typ `IPipelineFile`. Dieses File muss die XTF-Datei enthalten, welche validiert werden soll.

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `ErrorLog`: Ein Output-File vom Typ `IPipelineFile?`, welches das Error-Log der Validierung enthält. Dieses Log enthält alle Fehler, welche bei der Validierung aufgetreten sind. Kann `null` sein, wenn die Validierung bereits vor der Validierung mit dem IliValidator fehlschlägt. Weitere Informationen können in diesem Fall von `StatusMessage` entnommen werden.
- `XtfLog`: Ein Output-File vom Typ `IPipelineFile?`, welches das XTF-Log der Validierung enthält. Dieses Log enthält alle Informationen über die Validierung, wie z.B. die Anzahl der validierten Objekte, die Anzahl der Fehler, etc. Kann `null` sein, wenn die Validierung bereits vor der Validierung mit dem IliValidator fehlschlägt. Weitere Informationen können in diesem Fall von `StatusMessage` entnommen werden.
- `StatusMessage`: Eine lokalisierte Status-Nachricht der Validierung vom Typ `LocalizedText`, welche über den Status der Validierung informiert. Die Status-Nachricht kann auch ein leerer String sein.
- `ValidationSuccessful`: Wert vom Typ `bool`. Ist `true`, wenn das Input-File erfolgreich validiert werden konnte und valide ist. Ist `false`, wenn das Input-file nicht erfolgreich validiert werden konnte oder invalide ist.
