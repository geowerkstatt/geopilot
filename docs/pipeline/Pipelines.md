# Dokumentation und Konzepte im Zusammenhang mit geopilot und Pipelines

## Was ist eine Pipeline bei geopilot?

Eine Pipeline in geopilot ist eine Abfolge von Verarbeitungsschritten, die auf hochgeladene Daten angewendet werden, um ein bestimmtes Ergebnis zu erzielen.
Diese Schritte können verschiedene Operationen umfassen, wie z.B. Validierung, Transformation, Analyse oder Visualisierung.
Pipelines ermöglichen es, komplexe Datenverarbeitungsprozesse zu automatisieren und effizient zu gestalten.
Dabei können verschiedene Inputs und Outputs der Schritte miteinander verbunden werden, um die gewünschten Ergebnisse zu erzielen.

Die von der Pipeline verarbeiteten Daten können im Anschluss für die Datenlieferung verwendet werden.

## Wie ist eine Pipeline aufgebaut?

Alle Pipeline-Definitionen werden in einer gemeinsamen Pipeline-Konfigurationsdatei definiert. Pro Umgebung gibt es genau eine solche Konfigurationsdatei. Die Konfiguration wird in einer YAML-Datei erstellt. Eine Pipeline seinerseits enthält einen oder mehrere Schritte und beschreibt deren Konfigurationen und Logik. Der Aufbau einer Pipeline-Definition ist folgendermassen strukturiert:

- __Prozesse__: Eine Liste von möglichen Prozessen (processes), die in den Schritten (steps) verwendet werden können. Jeder Prozess hat eine eindeutige Identifikation (ID), über die er in den Schritten referenziert werden kann. Ein Prozess kann dabei in mehreren Schritten verwendet werden. Ein Prozess kann eine Standardkonfiguration haben, welche in den Schritten überschrieben werden kann. Jeder Prozess muss die Klasse, welche die Logik des Prozesses enthält, definieren.
- __Pipelines__: Eine Liste von Pipelines, welche die Verarbeitungsschritte beschreibt, die sequenziell ausgeführt werden. Jede Pipeline hat eine eindeutige Identifikation (ID) und eine Liste von Schritten.
  - __Schritte__: Eine Liste von Schritten, die in der Pipeline ausgeführt werden. Jeder Schritt hat eine innerhalb der Pipeline eindeutige Identifikation (ID). Des Weiteren definiert der Schritt, wie er an seine Daten kommt und wie er seine Ergebnisse zur Verfügung stellt. Eine Referenz auf einen Prozess definiert die Logik des Schrittes, welche ausgeführt wird. Die Konfiguration des Schrittes überschreibt die Standardkonfiguration des Prozesses, und definiert somit die spezifische Logik des Schrittes.

## Wer darf eine Pipeline ausführen?

Die Berechtigung zur Ausführung einer Pipeline wird über die Mandate geregelt. Ein solches Mandat kann entweder öffentlich sein oder über die Organisation einem Benutzer zugeordnet werden.

## Wie ist der Ablauf einer Pipeline-Ausführung?

Zu Beginn wählt der Anwender ein Mandat aus welches per ID auf eine Pipeline referenziert. Anhand dieser Referenz wird die Pipeline-Definition geladen. Jede Ausführung besitzt dabei seine eigene Pipeline-Instanz, welche die Informationen über die Ausführung der Pipeline enthält, wie z.B. die aktuellen Schritte und Prozessoren. Die Schritte innerhalb der Pipeline werden sequenziell ausgeführt, d.h. es wird erst mit dem nächsten Schritt begonnen, wenn der vorherige Schritt abgeschlossen ist.

### Status der Pipeline und Schritte

Schlägt ein Schritt fehl, so schlägt die gesamte Pipeline-Ausführung fehl und die darauffolgenden Schritte werden nicht ausgeführt. 

#### Schritte

Die Schritte können dabei verschiedene Stati durchlaufen, welche den aktuellen Stand der Schritt-Ausführung beschreiben. Diese Stati sind:

- `Pending`: Schritt wurde noch nicht gestartet, und befindet sich in der Warteschlange.
- `Skipped`: Schritt wurde aufgrund einer Condition übersprungen und nicht ausgeführt ([Bedingungen](#conditions-auf-schritten)).
- `Running`: Schritt ist aktuell in Bearbeitung.
- `Success`: Schritt wurde erfolgreich abgeschlossen.
- `Warning`: Schritt wurde ausgeführt, hat aber Probleme gemeldet (eine `warn_conditions`-POST-Condition traf zu). Die Pipeline läuft normal weiter; der Schritt gilt nicht als Fehler.
- `DeliveryRestriction`: Schritt wurde ausgeführt, schränkt aber die Datenlieferung ein (eine `restrict_delivery_conditions`-POST-Condition traf zu). Die Pipeline läuft normal weiter und der Schritt gilt nicht als Fehler, die produzierten Daten dürfen aber nicht geliefert werden.
- `Error`: Schritt ist durch eigene Verarbeitungslogik fehlgeschlagen.
- `Cancelled`: Schritt wurde vor Abschluss von aussen abgebrochen (z.B. Job-Timeout oder Shutdown des Hosts). Wird bewusst von `Error` unterschieden, weil der Schritt nicht aufgrund eines Verarbeitungsfehlers, sondern durch eine externe Unterbrechung beendet wurde.

#### Pipeline

Der Status der Pipeline ergibt sich aus den Stati der Schritte.

- `Pending`: Alle Schritte befinden sich im Status `Pending`.
- `Running`: Ein Schritt befindet sich im Status `Running`, und es gibt keinen Schritt im Status `Error`.
- `Success`: Alle Schritte haben mit dem Status `Success` oder `Skipped` abgeschlossen.
- `Warning`: Alle Schritte haben terminal abgeschlossen, mindestens einer im Status `Warning` und die übrigen `Success` oder `Skipped` (kein `Error`, kein `Cancelled`, kein `DeliveryRestriction`). Ein `Warning`-Status verhindert die Datenlieferung nicht von sich aus.
- `DeliveryRestriction`: Alle Schritte haben terminal abgeschlossen, mindestens einer im Status `DeliveryRestriction` und die übrigen `Success`, `Skipped` oder `Warning` (kein `Error`, kein `Cancelled`). Die Datenlieferung ist nicht möglich. Dieser Status hat Vorrang vor `Warning`.
- `Failed`: Ein Schritt hat mit dem Status `Error` abgeschlossen (die darauffolgenden Schritte wurden nicht mehr ausgeführt).
- `Cancelled`: Mindestens ein Schritt befindet sich im Status `Cancelled` und kein Schritt ist im Status `Error`. Tritt auf, wenn die Pipeline während der Ausführung von aussen abgebrochen wurde (z.B. Job-Timeout oder Shutdown des Hosts). Eine `Cancelled`-Pipeline verhindert die Datenlieferung.

### Datenfluss in der Pipeline

Ein Schritt und somit auch ein Prozess kann 1-n Inputs und 1-n Outputs haben. Die Inputs eines Schrittes werden von vorherigen Schritten bezogen. Ein Input kann zudem die hochgeladenen Dateien über `${upload()}` beziehen. Ebenso kann ein Input über eine Datei-Referenz `${file(pfad)}` eine Datei aus dem konfigurierten Ressourcen-Verzeichnis beziehen. Die Outputs eines Schrittes sind implizit alle öffentlichen, lesbaren Properties seines Prozess-Ergebnisses; sie stehen den darauffolgenden Schritten unter ihrem Property-Namen (PascalCase) zur Verfügung, ohne einzeln deklariert werden zu müssen.

Die Daten der Schritte werden in einem Pipeline-Context akkumuliert. Die Schritte beziehen ihren Input von diesem Pipeline-Context. Am Ende der Pipeline-Ausführung stehen die Ergebnisse zur Datenlieferung oder zum Download bereit. Welche Behandlung eine Ausgabe erhält, wird über `output_actions` gesteuert: dort wird einer Result-Property über ihren Namen eine oder mehrere Actions zugewiesen. Die folgenden Actions können definiert werden:

- `Download`: Die Daten werden zum Download bereitgestellt, und können von den Anwendern heruntergeladen werden. Der Wert muss ein `IPipelineFile` oder eine Sammlung davon (`IEnumerable<IPipelineFile>`) sein; andernfalls schlägt der Schritt zur Laufzeit fehl.
- `Delivery`: Die Daten werden in der Lieferung mit abgegeben. Der Wert muss ein `IPipelineFile` oder eine Sammlung davon (`IEnumerable<IPipelineFile>`) sein; ein anderer Wert wird ignoriert und es wird nichts geliefert.
- `StatusMessage`: Kann für die Bereitstellung von Statusnachrichten verwendet werden, welche in der Benutzeroberfläche angezeigt werden. Der Typ für Daten welche als StatusMessage bereitgestellt werden, ist `LocalizedText` (ab PipelineCore 1.3). Aus Gründen der Abwärtskompatibilität wird weiterhin auch ein `Dictionary<string, string>` akzeptiert. Key ist dabei der Sprachcode (z.B. `de`, `en`, `fr`, `it`), und Value die Nachricht in der entsprechenden Sprache.
- `Visualization`: Markiert die Ausgabe als Visualisierung. Der zugewiesene Wert muss ein `IVisualization`-Envelope sein (keine Datei); andernfalls schlägt der Schritt zur Laufzeit fehl. Die Runtime serialisiert ihn zu JSON (camelCase, `null`-Felder werden weggelassen), legt ihn im dedizierten Visualisierungs-Store ab und vermerkt eine Referenz auf dem Schritt, ausgeliefert wird er über den Visualisierungs-Endpunkt. Das JSON ist ein selbstbeschreibender Envelope `{ "type": <Diskriminator>, "data": <Payload> }`: `type` wählt die Frontend-Komponente, `data` ist der von ihr gerenderte Payload. Das konkrete `data`-Format ist prozessor-spezifisch und bei der jeweiligen Prozessor-Doku beschrieben. Diese Ausgabeaktion ist **eingebauten Prozessoren vorbehalten**: `IVisualization` ist nicht Teil des Plugin-Vertrags, und die darstellende Komponente liegt im geopilot-Frontend (siehe [Plugin-System](pluginSystem.md)).

Der Zugriff auf diese Daten erfolgt über eine Referenz in der Form `${step_output(stepId.PropertyName)}`: `stepId` wählt den vorherigen Schritt, von welchem die Daten bezogen werden, und `PropertyName` ist der Name der Result-Property (PascalCase) des Prozesses dieses Schrittes.

Die Daten können dabei verschiedene Typen haben. Dies können einfache Datentypen wie Strings oder Zahlen sein, aber auch komplexe Datentypen wie Dateien (z.B. `IPipelineFile`) oder Listen. Es ist dabei zu beachten, dass die Daten, welche von einem Schritt bereitgestellt werden, von den darauffolgenden Schritten verarbeitet werden können. Um dies zu gewährleisten muss die Dokumentation des jeweilig verwendeten Prozessors konsultiert werden. Dabei muss sowohl der Typ, als auch der Namen, unter welchem die Daten dem Prozessor bereitgestellt werden, übereinstimmen. Stimmt dies nicht überein, schlägt die Pipeline zu Laufzeit fehl.

Es ist möglich, dass die Daten von einem Schritt von mehreren darauffolgenden Schritten verarbeitet werden können. Ebenso ist es möglich, dass sich mehrere Schritte auf den gleichen Datensatz beziehen. Durch diese Möglichkeiten lassen sich Ausführungsstränge öffnen und wieder zusammenführen.

## Wie ist eine Pipeline-Definition aufgebaut?

```yaml
processes:
  - id: xtf_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
    default_config:
      fileExtensions:
        - xtf
  - id: xtf_validator
    implementation: Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess
    default_config:
      validationProfile: DEFAULT
pipelines:
  - id: xtf_validation
    display_name:
      en: XTF Validation
      de: XTF Validierung
      fr: XTF Validation
      it: XTF Validazione
    steps:
      - id: xtf_matching
        display_name:
          en: XTF Matching
          de: XTF Zuordnung
          fr: XTF Correspondance
          it: XTF Corrispondenza
        process_id: xtf_matcher
      - id: validation
        display_name:
          en: XTF Validation
          de: XTF Validierung
          fr: XTF Validation
          it: XTF Validazione
        process_id: xtf_validator
        process_config_overwrites:
          validationProfile: PROFILE-A
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
        output_actions:
          - property: ErrorLog
            actions:
              - Download
          - property: XtfLog
            actions:
              - Download
```

- `processes`: Liste von Prozessen, welche in den Schritten verwendet werden können.
  - `processes[X].id`: Die eindeutige ID des Prozesses, welche in den Schritten referenziert werden kann.
  - `processes[X].implementation`: Die Implementierung für den aktuellen Prozess.
  - `processes[X].default_config`: Optionale Standard-Konfiguration für den aktuellen Prozess. Die Konfigurationsmöglichkeiten eines spezifischen Prozesses müssen in der Dokumentation des Prozesses nachgeschlagen werden. Diese Konfiguration kann in den Schritten überschrieben werden, um spezifisches Verhalten zu definieren. In diesem konkreten Beispiel definiert die Standard-Konfiguration des XTF Validators (`processes[1]`) das Profil `DEFAULT` und ein Poll-Intervall von 1000ms. Beim XTF Matcher (`processes[0]`) wird die Dateiendung `xtf` als Filter konfiguriert.
- `pipelines`: Liste von Pipelines, welche die Verarbeitungsschritte beschreiben.
  - `pipelines[0].id`: Eindeutige ID der Pipeline, welche zur Ausführung der Pipeline verwendet wird. Diese ID wird als Referenz im Mandat verwendet, um die Berechtigung zur Ausführung der Pipeline zu regeln.
  - `pipelines[0].display_name`: Anzeigename der Pipeline, der in der Benutzeroberfläche verwendet wird. Wir übersetzen üblicherweise die folgenden Sprachen: Deutsch 'de', Englisch 'en', Französisch 'fr' und Italienisch 'it'.
  - `pipelines[0].steps`: Liste der Schritte, die in der Pipeline ausgeführt werden. In diesem Beispiel ist der erste Schritt (`steps[0]`) der XTF Matcher, welcher die hochgeladenen Dateien filtert, und der zweite Schritt (`steps[1]`) die XTF Validierung.
    - `pipelines[0].steps[X].id`: ID des Schrittes. Diese ID muss für jeden Schritt innerhalb einer Pipeline eindeutig sein. In diesem Beispiel ist die ID des Matcher-Schrittes `xtf_matching` und die ID des Validierungsschrittes `validation`.
    - `pipelines[0].steps[X].display_name`: Anzeigename des Schrittes, der in der Benutzeroberfläche verwendet wird. Wir übersetzen üblicherweise die folgenden Sprachen: Deutsch 'de', Englisch 'en', Französisch 'fr' und Italienisch 'it'.
    - `pipelines[0].steps[X].process_id`: Referenz auf die ID des Prozesses, welche die Logik des Schrittes definiert. In diesem Beispiel wird im Validierungsschritt (`steps[1]`) der Prozess `xtf_validator` verwendet, welcher in der Liste der Prozesse definiert ist (siehe `processes[1].id`).
    - `pipelines[0].steps[X].process_config_overwrites`: Überschreibt die Standard-Konfiguration des Prozesses, um spezifisches Verhalten für diesen Schritt zu definieren. In diesem Beispiel wird im Validierungsschritt (`steps[1]`) das `validationProfile` auf 'PROFILE-A' geändert.
    - `pipelines[0].steps[X].input`: Definiert, wie der Schritt an seine Eingabedaten kommt. Der Input ist eine Zuordnung (Map) von Prozessparameter-Namen zu Werten: Der Schlüssel ist der Name des Run-Methoden-Parameters, dem der Wert übergeben wird, der Wert ist die Quelle für diesen Parameter. Es können mehrere Parameter definiert werden, welche alle in der Dokumentation des verwendeten Prozessors beschrieben sein müssen. In diesem Beispiel wird im Validierungsschritt (`steps[1]`) der Parameter `transferFile` gesetzt, welcher den Output `XtfFiles` des Schrittes `xtf_matching` bezieht.
      - Schlüssel (im Beispiel `transferFile`): der Name des Prozessparameters, dem der Wert übergeben wird. Dieser Name muss der Dokumentation des verwendeten Prozesses entnommen werden und einen Parameter der Run-Methode treffen, sonst schlägt die Validierung beim Laden fehl.
      - Wert: entweder ein Literal (ein direkt geschriebener Wert wie `PROFILE-A`), oder eine Referenz auf den Output eines vorherigen Schrittes in der Form `${step_output(stepId.PropertyName)}`. Dabei ist `stepId` die ID eines Schrittes, welcher sich in der gleichen Pipeline vor dem aktuellen Schritt befindet, und `PropertyName` der Name der Result-Property (PascalCase) dieses Schrittes. Damit der Prozess den Wert korrekt verarbeiten kann, müssen Name und Typ übereinstimmen.
      - Mehrere Quellen auf denselben Parameter: Als Wert kann eine YAML-Liste von Einträgen (Referenzen und/oder Literale) angegeben werden, welche zu einem einzigen Parameter zusammengeführt werden. Voraussetzung ist, dass der Prozessparameter dafür ausgelegt ist (z.B. ein Array- bzw. `params`-Parameter).
      - Datei-Referenz: Als Wert kann eine Datei aus dem konfigurierten Ressourcen-Verzeichnis in der Form `${file(pfad)}` referenziert werden. Der Pfad ist relativ zum Ressourcen-Verzeichnis (Appsettings `Storage:ResourcesDirectory`), muss innerhalb dieses Verzeichnisses liegen (kein absoluter Pfad, keine `..`-Segmente) und wird dem Prozessparameter als `IPipelineFile` übergeben. Damit lässt sich eine konstante Datei (z.B. eine Vorlage oder eine Nachschlagetabelle) direkt injizieren, ohne dass ein vorheriger Schritt sie bereitstellen muss. Das Ressourcen-Verzeichnis wird vom Deployment bereitgestellt (z.B. als Volume gemountet).
    - `pipelines[0].steps[X].output_actions`: Optional. Weist einzelnen Result-Properties des Prozesses eine oder mehrere Actions zu. Die Outputs selbst müssen nicht deklariert werden: alle öffentlichen Properties des Prozess-Ergebnisses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen zur Verfügung. `output_actions` wird nur benötigt, wenn eine Property zusätzlich behandelt werden soll (herunterladbar, in der Lieferung, als Statusnachricht oder als Visualisierung). In diesem Beispiel werden vom Validierungsschritt (`steps[1]`) die Properties `ErrorLog` und `XtfLog` je zum Download bereitgestellt.
      - `pipelines[0].steps[X].output_actions[X].property`: Der Name der Result-Property des Prozesses (PascalCase), auf welche die Actions angewendet werden.
      - `pipelines[0].steps[X].output_actions[X].actions`: Die Liste der Actions für diese Property. Es kann eine oder mehrere Actions geben (z.B. `Download` und `Delivery` gemeinsam).

## Validierungen

Zum Programmstart wird validiert, ob die Pipeline-Definition korrekt ist. Dabei wird geprüft, ob die Definition den Anforderungen entspricht, und ob alle Referenzen korrekt sind. Es werden folgende Validierungen durchgeführt:

- Es muss mindestens ein Prozess vorhanden sein.
- Jeder Prozess muss eine eindeutige ID haben.
- Jeder Prozess muss eine Implementierung haben, welche die Logik des Prozesses enthält. Diese Implementierung muss in der Anwendung vorhanden sein, damit der Prozess verwendet werden kann (entweder als mit geopilot ausgelieferter Prozessor oder als über `Pipeline:Plugins` geladenes Plugin-Assembly).
- Es muss mindestens eine Pipeline vorhanden sein.
- Jede Pipeline muss eine eindeutige ID haben.
- Jeder Schritt muss eine eindeutige ID innerhalb der Pipeline haben.
- Jeder Schritt muss eine gültige Referenz auf einen Prozess haben, welcher in der Liste der Prozesse definiert ist.
- Eine Input-Referenz `${step_output(stepId.PropertyName)}` muss syntaktisch korrekt sein und auf einen in der Pipeline definierten Schritt zeigen.
- Eine Input-Referenz darf nur auf vorgängige Schritte zeigen und nicht auf Schritte, welche nach dem aktuellen Schritt kommen.
- Ob die referenzierte Property auf dem Ergebnistyp des referenzierten Schrittes tatsächlich existiert, wird nicht beim Laden geprüft, sondern erst zur Laufzeit (die Outputs sind implizit und werden per Reflection aufgelöst). Stimmt der Name mit keiner Property überein, schlägt der Schritt zur Laufzeit fehl.
- Jeder Input-Schlüssel muss einen Parameter der Run-Methode des Prozesses treffen (der `CancellationToken` wird nicht über den Input verdrahtet). Ein Schlüssel, welcher keinen solchen Parameter trifft, lässt die Validierung fehlschlagen.
- Ein als Literal geschriebener Input-Wert muss sich in den Typ des Zielparameters konvertieren lassen.
- Eine Datei-Referenz `${file(pfad)}` muss einen relativen Pfad ohne `..`-Segmente verwenden, so dass sie nicht aus dem Ressourcen-Verzeichnis ausbrechen kann.
- Der Zielparameter einer Datei-Referenz muss ein `IPipelineFile` (oder eine Liste davon) entgegennehmen, und die referenzierte Datei muss im konfigurierten Ressourcen-Verzeichnis vorhanden sein.
- Innerhalb der `output_actions` eines Schrittes darf dieselbe `property` nicht mehrfach vorkommen.
- Ein Schritt darf höchstens eine Property mit der Action `StatusMessage` taggen.
- Ein Schritt darf nur Konfigurationsparameter überschreiben, welche auf der `default_config` des Prozesses definiert sind.
- Die Pipeline-Definition darf keinen Konfigurationsschlüssel setzen, welcher bereits in der Basis-Konfiguration (`Pipeline:ProcessConfigs`) gesetzt ist, siehe [Kollision mit der Basis-Konfiguration](#kollision-mit-der-basis-konfiguration).

## Konfiguration eines Pipeline-Prozessors

Die Konfiguration eines Pipeline-Prozessors erfolgt über die Appsettings von geopilot und über die Pipeline-Definition. Die Namen der Konfiguration müssen der Dokumentation des jeweiligen Prozessors entnommen werden. Der Name der Konfiguration entsprich dabei immer dem Namen des Konstruktorparameters des Prozessors. Ein Prozessor muss genau einen `public` Konstruktor besitzen.

### Konfigurationsmöglichkeiten

Eine Konfiguration besteht immer aus einem Key-Value-Paar, wobei der Key der Name der Konfiguration ist, und der Value der Wert der Konfiguration ist. Die Konfigurationsparameter aus den unterschiedlichen Quellen (Appsettings, Standard-Konfiguration - auf dem Prozessor, überschriebene Konfiguration - auf dem Schritt) werden zu einer Konfiguration zusammengeführt, aber mit klarer Priorität und Restriktionen.

#### Parameter-Typen

Alle Typen von Konfigurationsparameter sind sowohl als Pflicht, als auch als optionale Parameter möglich. Ist ein Pflicht-Parameter in der Initialisierung eines Prozessors definiert, und kann dieser Parameter nicht aus der Konfiguration bezogen werden, schlägt die Pipeline-Ausführung fehl. Optionale Parameter können in der Initialisierung eines Prozessors weggelassen werden, und müssen somit nicht zwingend aus der Konfiguration bezogen werden. Damit der Parameter korrekt der Initialisierung eines Prozessors zugeordnet werden kann, müssen sowohl der Name als auch der Typ des Parameters übereinstimmen.

Es gibt folgende mögliche Typen von Konfigurationsparametern:

- `string`: Ein String-Parameter, welcher einen beliebigen Text enthalten kann. Beispiel: Basis-URL eines Drittanbieter-Services, Filename eines durch den Prozessor generierten Files, Validierungsprofil, ...
- `int`: Ein Integer-Parameter, welcher eine Ganzzahl enthält. Beispiel: Poll-Intervall in Millisekunden, Anzahl der zu verarbeitenden Objekte, ...
- `double`: Ein Double-Parameter, welcher eine Gleitkommazahl enthält. Beispiel: Schwellenwert, ...
- `bool`: Ein Bool-Parameter, welcher einen Wahrheitswert (true/false) enthält. Beispiel: Aktivierungsstatus, Debugging-Optionen, ...

#### Appsettings

1. **Prozessor-Typ**: Der vollqualifizierte Prozessor für welchen die Konfiguration gilt. Der Schlüssel ist der Implementierungs-Typ und nicht die Prozess-Id, ein Eintrag gilt also für jeden `processes:`-Eintrag der Pipeline-Definition, welcher diese Implementierung nennt.
2. **Prozessor-Parameter**: Key-Value-Paar der Parameter.

Ein Beispiel für die Konfiguration eines Pipeline-Prozessors in den Appsettings könnte wie folgt aussehen:

```json
{
  "Pipeline": {
    "ProcessConfigs": {
      "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess": {
        "modelDirs": "https://models.example.com/;https://models.interlis.ch/"
      }
    }
  }
}
```

Im Betrieb wird derselbe Wert in der Regel nicht als JSON, sondern als Umgebungsvariable gesetzt. Der Trenner ist dann `__` statt `:`:

```
Pipeline__ProcessConfigs__Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess__modelDirs=https://models.example.com/;https://models.interlis.ch/
```

Das ist auch das typische Beispiel für die Wahl der Schicht: die Modell-Repositories bestimmen, wogegen validiert wird, gelten pro Umgebung und sollen von einer Pipeline nicht verändert werden können. Sie gehören deshalb in die Basis-Konfiguration und nicht in die Pipeline-Definition.

#### Pipeline-Definition

- `processes[X].default_config`: Standard-Konfiguration für einen Prozess, welche in den Schritten überschrieben werden kann.
- `pipelines[X].steps[X].process_config_overwrites`: Überschreibt die Standard-Konfiguration des Prozesses, um spezifisches Verhalten für diesen Schritt zu definieren.

Ein Beispiel für die Konfiguration eines Pipeline-Prozessors in der Pipeline könnte wie folgt aussehen:
 
```yaml
processes:
  - id: xtf_validator
    implementation: Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess
    default_config:
      validationProfile: DEFAULT
pipelines:
  - id: xtf_validation
    display_name:
      en: XTF Validation
    steps:
      - id: validation
        display_name:
          en: XTF Validation
        process_id: xtf_validator
        process_config_overwrites:
          validationProfile: PROFILE-A
```

### Regeln für die Konfiguration

Die Übernahme eines Konfigurationsparameters ist hierarchisch und wird folgendermassen priorisiert:

1. **Basis-Konfiguration**: Die Basis-Konfiguration eines Prozessors, welche in den Appsettings definiert ist, bildet die Grundlage für die Konfiguration des Prozessors.
2. **Standard-Konfiguration**: Die Standard-Konfiguration, welche in der Pipeline-Definition unter `processes[X].default_config` definiert ist
3. **Schritt-spezifische Konfiguration**: Die Schritt-spezifische Konfiguration, welche in der Pipeline-Definition unter `pipelines[X].steps[X].process_config_overwrites` definiert ist, überschreibt die Standard-Konfiguration für diesen spezifischen Schritt.

Für das Überschreiben von Konfigurationsparametern gelten folgende Einschränkungen:

- Basis-Konfigurationen, welche in den Appsettings definiert sind, können nicht von der Pipeline-Definition überschrieben werden. Ein in der Basis-Konfiguration definierter Wert ist somit fix und unveränderbar; der Versuch, ihn aus der Definition zu setzen, lässt die Applikation nicht starten (siehe [Kollision mit der Basis-Konfiguration](#kollision-mit-der-basis-konfiguration)). Ein Beispiel für einen solchen Konfigurationsparameter ist die Basis-URL eines Drittanbieter-Services, welcher für die gesamte Umgebung gilt (Development, Acceptance, Prod, ...). Um die gleiche Pipeline auf den verschiedenen Umgebungen mit unterschiedlichen Basis-URLs verwenden zu können, sollte dieser Parameter in den Appsettings definiert werden und nicht in der Pipeline-Definition.
- Das Überschreiben von Konfigurations-Parameter wird in den Schritten unter `pipelines[X].steps[X].process_config_overwrites` vorgenommen. Um ein Konfigurationsparameter zu überschreiben muss dieser Parameter auf der Standard-Konfiguration des Prozesses definiert sein (`processes[X].default_config`). Es ist somit nicht möglich, neue Konfigurationsparameter in den Schritten zu definieren, welche nicht bereits in der Standard-Konfiguration definiert sind.

Zu den Typen: Listen können nur in der Pipeline-Definition angegeben werden, die Appsettings-Ebene trägt nur Einzelwerte. Ein Parameter, der unveränderbar sein soll und trotzdem mehrere Werte braucht, wird deshalb als Einzelwert entworfen (der Prozessor `xtf_validator` nimmt seine Modell-Repositories zum Beispiel als semikolon-getrennten Wert).

#### Kollision mit der Basis-Konfiguration

Die Unveränderbarkeit der Basis-Konfiguration ist keine stille Vorrangregel, sondern wird beim Laden der Pipeline-Definition durchgesetzt: Steht derselbe Schlüssel in der Basis-Konfiguration **und** in der Pipeline-Definition (in `default_config` oder in `process_config_overwrites`), startet die Applikation nicht. Die Meldung nennt beide Orte:

```
errors in pipeline definition:
PipelineProcessConfig: Process configuration collision for implementation
'Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess': the key 'validationProfile' is set in the
base configuration (app settings 'Pipeline:ProcessConfigs:Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess:validationProfile',
environment variable 'Pipeline__ProcessConfigs__Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess__validationProfile')
and in the pipeline definition ('processes[id=xtf_validator].default_config.validationProfile'). The base
configuration cannot be overridden. Remove the key from the pipeline definition, or remove it from the base
configuration if the value has to be set per pipeline.
```

Zu entfernen ist im Regelfall der Eintrag in der Pipeline-Definition, denn die Basis-Konfiguration ist bewusst nicht übersteuerbar. Soll der Wert dagegen pro Pipeline unterschiedlich sein, gehört er umgekehrt nicht in die Basis-Konfiguration. Die beiden Schichten werden typischerweise von verschiedenen Personen gepflegt (die Basis-Konfiguration im Hosting, die Definition in der gemounteten YAML), deshalb nennt die Meldung sowohl die Umgebungsvariable als auch den Pfad in der Definition.

Dass eine Kollision die Applikation anhält, statt dass die Basis-Konfiguration einfach gewinnt, ist Absicht: Ein Schlüssel gehört genau einer Schicht. Betriebs- und sicherheitsrelevante Parameter (URLs, Pfade, Hosts, Tokens, Timeouts) gehören ausschliesslich in die Basis-Konfiguration, wo keine Pipeline-Definition sie erreicht; alles, was pro Pipeline oder pro Schritt variiert, gehört ausschliesslich in die Definition. Steht ein Schlüssel auf beiden Schichten, ist diese Zuordnung falsch. Würde die Basis-Konfiguration stattdessen nur mit einer Warnung gewinnen, bliebe der Schlüssel über die `default_config` weiterhin als überschreibbar deklariert, und sobald die Basis-Konfiguration einmal fehlt (neue Umgebung, aufgeräumtes Compose-File), wäre er ohne Codeänderung überschreibbar.

#### Dateien als Konfiguration

Ein Konfigurationsparameter vom Typ `IPipelineFile` nennt eine **Datei, die das Deployment mitbringt**. Konfiguriert wird ihr Pfad relativ zum Ressourcen-Verzeichnis (Appsettings `Storage:ResourcesDirectory`), also derselben Wurzel, gegen die eine `${file(...)}`-Referenz im `input` auflöst:

```json
{
  "Pipeline": {
    "ProcessConfigs": {
      "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess": {
        "modelRepository": "model-repository.zip"
      }
    }
  }
}
```

Der Pfad muss innerhalb der Wurzel liegen und eine existierende Datei nennen, sonst startet die Applikation nicht. Der Unterschied zum `${file(...)}`-Input ist nicht die Datei, sondern wer sie bestimmt: als Konfiguration lässt sie sich in der Basis-Konfiguration unveränderbar festlegen, als Input wählt sie der Autor der Pipeline-Definition pro Schritt. Für eine konstante Datei, die nicht aus dem Ergebnis eines vorherigen Schrittes stammen kann, ist die Konfiguration der richtige Ort.

### Beispiel einer Instanziierung eines Prozessors mit Konfigurationsparametern

Das folgende Beispiel zeigt die Initialisierung des `XtfValidatorProcess` welcher mit geopilot ausgeliefert wird. Es werden die Konfigurationsparameter `validationProfile`, `modelDirs` und `allObjectsAccessible` übergeben, alle optional: das Profil, anhand dessen die Validierung durchgeführt wird, die Modell-Repositories, aus denen Modelle und Profil aufgelöst werden, und ob Verweise auf Objekte ausserhalb der geprüften Datei als Fehler gelten. Alle drei sind Einzelwerte und lassen sich damit in beiden Schichten setzen, `modelDirs` als semikolon-getrennter Wert.

Der vierte, `modelRepository`, ist vom Typ `IPipelineFile` und nennt eine Datei des Deployments, siehe [Dateien als Konfiguration](#dateien-als-konfiguration).

Der `logger` ist nicht Teil der Konfiguration, sondern wird von geopilot bereitgestellt, um innerhalb des Prozesses wichtige Informationen zu loggen. Es wird empfohlen den Logger von geopilot zu verwenden, anstatt einen eigenen Logger zu erstellen, um die Konsistenz der Logs zu gewährleisten und die Logs korrekt in die Log-Management-Lösung von geopilot zu integrieren.

Der `pipelineFileManager` ist ebenfalls nicht Teil der Konfiguration, sondern wird von geopilot bereitgestellt, um das Erstellen von Dateien innerhalb der Pipeline zu ermöglichen. Dabei wir sichergestellt, dass Dateien, welche mit dem `pipelineFileManager` erstellt wurden, nach dem Terminieren der Pipeline wieder abgeräumt werden.

Der `ilivalidatorClient` wird ebenso von geopilot bereitgestellt und ruft den konfigurierten ilitools-wrapper auf. Prozessoren, welche INTERLIS-Werkzeuge brauchen, fordern einen solchen Client im Konstruktor an, anstatt selbst einen Dienst anzusprechen.

```csharp
public XtfValidatorProcess(string? validationProfile, string? modelDirs, bool? allObjectsAccessible, IPipelineFile? modelRepository, IIlivalidatorClient ilivalidatorClient, IPipelineFileManager pipelineFileManager, ILogger logger)
{
}
```

## Conditions

Conditions ermöglichen es, die Ausführung von Schritten und somit der Pipeline oder die Bereitstellung von Daten zu steuern. Dabei wird eine Condition definiert, welche bei der Ausführung der Pipeline ausgewertet wird. Eine Condition referenziert dabei auf Resultate von vorherigen Schritten.

Beispiel für Conditions in einer Pipeline-Definition:

```yaml
processes:
  - id: xtf_matcher
    implementation: Geopilot.Pipeline.Processes.Matcher.XtfMatcher.XtfMatcherProcess
    default_config:
      fileExtensions:
        - xtf
  - id: xtf_validator
    implementation: Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess
  - id: zip_package_process
    implementation: Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess
pipelines:
  - id: xtf_validation
    display_name:
      en: XTF Validation
    steps:
      - id: xtf_matching
        display_name:
          en: XTF Matching
        process_id: xtf_matcher
      - id: validation
        display_name:
          en: XTF Validation
        process_id: xtf_validator
        conditions:
          post:
            restrict_delivery_conditions:
              - expression: "!([validation.ValidationSuccessful])"
                message:
                  de: "Die Validierung war nicht erfolgreich. Datenlieferung nicht möglich."
                  en: "Validation was not successful. Delivery is not possible."
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
      - id: zip_package
        display_name:
          en: Zip Package
        process_id: zip_package_process
        conditions:
          pre:
            skip_conditions:
              - expression: "[validation.ErrorLog] != null && [validation.XtfLog] != null"
                message:
                  de: "Schritt übersprungen, da Logs vorhanden."
                  en: "Step skipped because logs exist."
            fail_conditions:
              - expression: "[validation.ErrorLog] != null && [validation.XtfLog] != null"
                message:
                  de: "Schritt fehlgeschlagen, da Logs vorhanden."
                  en: "Step failed because logs exist."
          post:
            fail_conditions:
              - expression: "[zip_package.ZipPackage] == null"
                message:
                  de: "Zip-Paket konnte nicht erstellt werden."
                  en: "Zip package could not be created."
        input:
          input:
            - "${step_output(validation.ErrorLog)}"
            - "${step_output(validation.XtfLog)}"
```

### Syntax der Expressions

Als Basis dient [NCalc](https://ncalc.github.io/ncalc/articles/index.html). Dabei muss beachtet werden, dass die Expression ein Boolscher Ausdruck sein muss, welcher bei der Auswertung entweder zu `true` oder `false` ausgewertet wird. Es können dabei die Resultate von vorherigen Schritten referenziert werden, indem auf die Schritt-ID (`steps.id`) und den Property-Namen des Outputs (`step.PropertyName`) verwiesen wird. Diese referenzierten Parameter können dann in der Expression verwendet werden, um die Condition zu definieren. Parameter werden dabei in eckigen oder geschweiften Klammern referenziert (`[step.result]` oder `{step.result}`).

Folgende Syntax-Elemente können in den Expressions verwendet werden:

- AND- oder OR-Logik: `&&` oder `and` für AND, `||` oder `or` für OR
- Negieren: `!`, `-` oder `not`
- Vergleichsoperatoren: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Klammern zur Gruppierung von Bedingungen: `(...)`

Eine Detaillierte Übersicht über die Syntax-Elemente und deren Verwendung kann in der [NCalc-Dokumentation](https://ncalc.github.io/ncalc/articles/index.html) nachgeschlagen werden.

#### Funktionen

Zusätzlich zu den Standard-NCalc-Funktionen stellt geopilot folgende eigene Funktionen bereit:

- `Length(parameter)`: Gibt die Anzahl Elemente einer Sammlung (Array oder Collection) zurück. Kann verwendet werden, um die Grösse von Resultaten vorheriger Schritte zu prüfen. Wirft einen Fehler, wenn der Parameter `null` ist oder kein Array bzw. keine Collection ist.

#### Beispiele für die Syntax

- Einfache Condition: `[validation.ValidationSuccessful] == true`
- Mehrere Bedingungen: `[validation.ValidationSuccessful] == true && [validation.ErrorLog] == null`
- Bedingung mit OR-Logik: `[validation.ValidationSuccessful] == true || [validation.ErrorLog] == null`
- Bedingung Klammern: `[validation.ValidationSuccessful] == true && ([validation.ErrorLog] == null || [validation.XtfLog] == null)`
- Anzahl Elemente prüfen: `Length([xtf_matching.XtfFiles]) == 1`
- Kombination mit Length: `Length([xtf_matching.XtfFiles]) > 0 && [validation.ValidationSuccessful] == true`

### Conditions auf Schritten

Conditions auf Schritten ermöglichen es, die Ausführung eines Schrittes zu steuern. Jede Condition ist ein Objekt mit den folgenden Eigenschaften:

- `expression`: Der boolsche Ausdruck, welcher ausgewertet wird (Pflichtfeld).
- `message`: Ein optionaler lokalisierter Text (`LocalizedText`, Key = Sprachcode, Value = Nachricht). Diese Nachrichten werden als Statusnachricht des Schrittes bereitgestellt.

Es können pro Bedingungstyp (`skip_conditions`, `fail_conditions`, `restrict_delivery_conditions`, `warn_conditions`) mehrere Conditions definiert werden. Die Conditions werden mit ODER-Logik ausgewertet: Trifft mindestens eine Condition zu, wird die entsprechende Aktion (Skip, Error, DeliveryRestriction oder Warning) ausgelöst. Dabei werden **alle** zutreffenden Conditions gesammelt und deren Nachrichten pro Sprache kommasepariert zusammengeführt.

#### PRE-Condition

In einer PRE-Condition wird vor der Ausführung eines Schrittes ausgewertet und entschieden, ob der Schritt ausgeführt oder mit einem Fehler abgebrochen werden soll. Die `fail_conditions` werden dabei vor den `skip_conditions` ausgewertet. Trifft eine Condition zu, resultiert das in den folgenden möglichen Szenarien:

- **Skipped**: Schritt wird aufgrund einer `skip_conditions`-Condition übersprungen und nicht ausgeführt. In der Pipeline wird ein übersprungener Schritt und ein erfolgreich abgeschlossener Schritt gleich behandelt. Es ist somit möglich, dass ein Schritt übersprungen wird, und die Pipeline trotzdem mit Erfolg abschliesst.
- **Error**: Schritt wird aufgrund einer `fail_conditions`-Condition mit einem Fehler abgebrochen. Dies führt dazu, dass die gesamte Pipeline mit einem Fehler abschliesst, und die darauffolgenden Schritte nicht mehr ausgeführt werden.

#### POST-Condition

In einer POST-Condition wird nach der Ausführung eines Schrittes ausgewertet, wie der Schritt abgeschlossen wird. Es gibt drei Bedingungstypen mit fester Priorität:

- `fail_conditions`: Trifft eine zu, wird der Schritt mit `Error` abgebrochen; die Pipeline schlägt fehl und die darauffolgenden Schritte werden nicht mehr ausgeführt.
- `restrict_delivery_conditions`: Trifft keine `fail_conditions`, aber mindestens eine `restrict_delivery_conditions`-Condition zu, schliesst der Schritt mit `DeliveryRestriction` ab. Der Schritt gilt nicht als Fehler, die Pipeline läuft normal weiter, die produzierten Daten dürfen aber nicht geliefert werden. Damit lassen sich datenabhängige Lieferbedingungen ausdrücken (z.B. eine nicht erfolgreiche Validierung), welche die Lieferung verhindern, ohne die Pipeline abzubrechen.
- `warn_conditions`: Trifft keine `fail_conditions` und keine `restrict_delivery_conditions`, aber mindestens eine `warn_conditions`-Condition zu, schliesst der Schritt mit `Warning` ab. Der Schritt gilt nicht als Fehler, die Pipeline läuft normal weiter. Damit lassen sich Probleme sichtbar machen, ohne die Pipeline abzubrechen. Die Datenlieferung wird dadurch nicht verhindert.

Die Precedence ist somit `Error` (fail) vor `DeliveryRestriction` (restrict_delivery) vor `Warning` (warn) vor `Success`.

```yaml
restrict_delivery_conditions:
  - expression: "!([validation.ValidationSuccessful])"
    message:
      de: "Die Validierung war nicht erfolgreich. Datenlieferung nicht möglich."
      en: "Validation was not successful. Delivery is not possible."
      fr: "La validation n'a pas réussi. La livraison n'est pas possible."
      it: "La validazione non è riuscita. La consegna non è possibile."
```

#### Statusnachrichten aus Conditions

Wenn zutreffende Conditions eine `message`-Eigenschaft besitzen, werden deren Nachrichten als Statusnachricht des Schrittes bereitgestellt. Dies ermöglicht es, den Anwendern in der Benutzeroberfläche lokalisierte Hinweise anzuzeigen, warum ein Schritt übersprungen oder fehlgeschlagen ist. Bei mehreren zutreffenden Conditions werden die Nachrichten pro Sprachcode kommasepariert zusammengeführt.

Beispiel einer Condition mit Nachricht:

```yaml
fail_conditions:
  - expression: "Length([xtf_matching.XtfFiles]) != 1"
    message:
      de: "Es muss genau eine XTF-Datei hochgeladen werden."
      en: "Exactly one XTF file must be uploaded."
      fr: "Exactement un fichier XTF doit être téléchargé."
      it: "Deve essere caricato esattamente un file XTF."
```

### Einschränkungen für die Datenlieferung (Delivery Restrictions)

Ob die produzierten Daten geliefert werden dürfen, wird über den Schritt-Status `DeliveryRestriction` gesteuert. Ein Schritt, dessen `restrict_delivery_conditions`-POST-Condition zutrifft (siehe [POST-Condition](#post-condition)), schliesst mit `DeliveryRestriction` ab. Dieser Status verdichtet sich auf die Pipeline (der Pipeline-Status wird `DeliveryRestriction`), womit die Datenlieferung nicht möglich ist. Der Default ist, dass eine Datenlieferung möglich ist; sie wird nur verhindert, wenn mindestens ein Schritt sie einschränkt oder die Pipeline fehlschlägt bzw. abgebrochen wird.

Die Einschränkung wird auf dem Schritt definiert, der den ausschlaggebenden Wert produziert (typischerweise die Validierung). Die Bedingung verwendet dieselbe Objektstruktur wie die übrigen Conditions (`expression` und optionale `message`) und referenziert Werte im gleichen Scope wie andere POST-Conditions (eigene Outputs und Outputs früherer Schritte). Die Nachrichten aller zutreffenden Einschränkungen werden pro Sprache kommasepariert zusammengeführt und dem Anwender in der Benutzeroberfläche als Grund angezeigt.

Beispiel:

```yaml
steps:
  - id: validation
    process_id: xtf_validator
    conditions:
      post:
        restrict_delivery_conditions:
          - expression: "!([validation.ValidationSuccessful])"
            message:
              de: "Die Validierung war nicht erfolgreich. Datenlieferung nicht möglich."
              en: "Validation was not successful. Delivery is not possible."
              fr: "La validation n'a pas réussi. La livraison n'est pas possible."
              it: "La validazione non è riuscita. La consegna non è possibile."
    # ...
```

> **Hinweis:** Schlägt die Pipeline fehl (Status `Failed`) oder wird sie abgebrochen (Status `Cancelled`), wird die Datenlieferung automatisch verhindert, unabhängig von den `restrict_delivery_conditions`.

## Prozesse

Dokumentation der Funktionsweise der Prozesse, welche mit geopilot ausgeliefert werden, und wie sie in den Pipelines verwendet werden können. Jeder mitgelieferte Prozessor ist in einer eigenen Datei dokumentiert:

- [XTF Matcher](Prozessoren/xtf-matcher.md)
- [File Matcher](Prozessoren/file-matcher.md)
- [XTF Validierung](Prozessoren/xtf-validierung.md)
- [ZIP Paketierung](Prozessoren/zip-paketierung.md)
- [ZIP Unpacker](Prozessoren/zip-unpacker.md)
- [XTF Fehlervisualisierung](Prozessoren/xtf-fehlervisualisierung.md)
