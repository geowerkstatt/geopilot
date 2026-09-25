# XTF Metadaten-Extraktor

Liest Metadaten einer Lieferung aus ihrer Transferdatei, damit nachfolgende Schritte damit arbeiten können. In dieser Fassung ist das der Validierungsumfang (`scope`), bei DMAV die BFS-Nummer der gelieferten Gemeinde, den die [XTF Validierung](xtf-validierung.md) über ihren Input `scope` entgegennimmt. Weitere Metadaten kommen als eigene Konfigurationsschlüssel und Outputs dazu; alle werden im selben Schritt und in einem einzigen Durchgang durch die Datei gelesen.

Unterstützt werden Transferdateien in INTERLIS 2.4. Die Datei wird Stück für Stück gelesen, auch eine grosse Lieferung braucht deshalb nur Speicher für die Objekte, die der Prozess tatsächlich ansieht.

## Implementierung

Ein XTF Metadaten-Extraktor muss unter `processes[X].implementation` den Wert `Geopilot.Pipeline.Processes.XtfMetadata.XtfMetadataExtractorProcess` definieren.

## Konfiguration

- `scope`: Woher der Validierungsumfang kommt, ein Objekt mit den Schlüsseln `path` und `fetch`. Weil es verschachtelt ist, gehört es in die Pipeline-Definition (`default_config` oder `process_config_overwrites`); die Basis-Konfiguration in den appsettings trägt nur einfache Werte. Ein `process_config_overwrites` ersetzt `scope` als Ganzes, `path` muss dort also wiederholt werden. Fehlt `scope` oder ist es leer, etwa weil `path` und `fetch` nicht darunter eingerückt sind, startet die Anwendung nicht, weil der Schritt nichts zu lesen hätte.
  - `path`: Pflicht. Ein XPath-Ausdruck, der auf jedes Objekt der Transferdatei angewendet wird. Objekte sind die direkten Kinder eines Korbs; ein gleichnamiges Element weiter unten, etwa der Verweis auf die Gemeinde in einer Gemeindegrenze, zählt nicht. Der erste Schritt nennt die Klasse so, wie die Transferdatei ihre Objekte schreibt, etwa `DMAV_HoheitsgrenzenAV_V1_0:Gemeinde` oder, mit Topic, `DMAV_Bodenbedeckung_V1_0:Bodenbedeckung.Bodenbedeckung`; der Pfad wird nur auf Objekte dieser Klasse angewendet. Ein Präfix steht für den Namensraum des gleichnamigen Modells, `ili` für den INTERLIS-Namensraum. Jedes gewählte Element oder Attribut mit nicht leerem Text ist ein Treffer, sein Text ohne Leerzeichen am Rand der Wert. Der Ausdruck muss Elemente oder Attribute wählen, ein berechneter Wert wie `count(...)` ist nicht erlaubt.
  - `fetch`: Optional, wie aus den Treffern ein Wert wird:
    - `Single` (Standard): Über die ganze Datei gibt es genau einen Treffer. Kein Treffer oder mehrere lassen `Scope` leer; zwei Treffer mit demselben Text sind zwei Treffer.
    - `First`: der erste Treffer in der Reihenfolge der Datei. Danach wird nicht weitergelesen.
    - `CurrentlyValid`: die Regel `VIEW Gemeinde_Gueltig` aus DMAV, anwendbar auf jede DMAV-Klasse mit demselben Muster. Gültig ist ein Objekt, dessen `Entstehung` auf eine Nachführung mit `GenehmigtAm` verweist und dessen `Untergang` auf keine. Über alle gültigen Objekte muss es genau einen Treffer geben. Eine Nachführung, die nicht in der Datei steht, gilt als nicht genehmigt.

Fehlt `path` oder steht in `scope` ein unbekannter Schlüssel, etwa ein vertippter `fetch`, verhindert das schon den Start der Anwendung. Für `fetch` sind nur die drei Namen oben gültig, in beliebiger Gross- und Kleinschreibung. Ein leerer oder fehlerhafter Pfad, etwa ein ungültiger XPath oder einer ohne Modell und Klasse im ersten Schritt, fällt erst beim ersten Lauf auf: Der Schritt scheitert mit einer Meldung, die den Pfad nennt.

### Grenzen

Die Transferdatei kommt vom Lieferanten, darum setzt der Prozess Grenzen. Ein gelesenes Objekt, das tiefer als 64 Ebenen verschachtelt ist oder mehr als 16'777'216 Zeichen enthält (jedes Element, jedes Attribut und jeder Text zählt dabei zusätzlich 16 Zeichen), macht die Datei unlesbar (`FileState` `Unreadable`); echte DMAV-Objekte bleiben weit darunter. Ebenso unlesbar ist eine Datei, deren gelesene Objekte mehr als 2'000 verschiedene Element- und Attributnamen verwenden oder einen Namen oder Namensraum mit mehr als 128 Zeichen; ein vollständiger DMAV-Datensatz kommt mit rund 300 Namen von höchstens 29 Zeichen aus. Die Tiefengrenze gilt auch für Objekte, die der Prozess nur überspringt, und für den Kopf der Datei. Mit `CurrentlyValid` liest der Prozess jedes Objekt, dort gelten die Grenzen also für alle Objekte der Datei. Ein gelesener Wert mit mehr als 255 Zeichen oder mit Steuerzeichen, auch unsichtbaren wie einem Zeilentrenner oder einem Richtungswechsel, taugt nicht als Scope, `Scope` bleibt dann leer.

### Welche Gemeinde bei DMAV?

Eine DMAV-Lieferung kann mehrere Gemeinden enthalten, etwa nach einer Fusion. Zwei Regeln sind üblich, beide sind Konfiguration:

| Regel | `path` | `fetch` |
| --- | --- | --- |
| gültig nach dem Modell | `DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer` | `CurrentlyValid` |
| ohne Untergang | `DMAV_HoheitsgrenzenAV_V1_0:Gemeinde[not(DMAV_HoheitsgrenzenAV_V1_0:Untergang)]/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer` | `Single` |

Die beiden unterscheiden sich nur, solange eine Änderung noch nicht genehmigt ist, etwa bei einer laufenden Fusion: Die Regel des Modells wählt dann die bisherige Gemeinde, die Regel ohne Untergang schon die neue.

## Input

Der Name des Prozessor-Inputs, welcher als Schlüssel im `input`-Map des Schrittes (`pipelines[X].steps[X].input`) verwendet werden muss.

- `transferFile`: Die Transferdatei vom Typ `IPipelineFile`, genau eine. In der Regel mit `${step_output(xtf_matching.XtfFiles)}` verdrahtet, zusammen mit der Vorbedingung, dass genau eine XTF-Datei geliefert wurde (siehe Beispiel).

## Output

Die öffentlichen Result-Properties des Prozesses stehen den nachfolgenden Schritten implizit unter ihrem Property-Namen (PascalCase) zur Verfügung und werden über `${step_output(stepId.PropertyName)}` referenziert. Soll eine Property zusätzlich behandelt werden (Download, Lieferung, Statusnachricht oder Visualisierung), wird sie in `pipelines[X].steps[X].output_actions` getaggt.

- `Scope`: Der gelesene Validierungsumfang vom Typ `string`, oder `null`, wenn es keinen eindeutigen Wert gibt. Wird mit `${step_output(stepId.Scope)}` auf den Input `scope` der XTF Validierung verdrahtet.
- `FileState`: Wie weit die Datei gelesen werden konnte:
  - `Readable`: ein INTERLIS-2.4-Transfer, gelesen so weit wie nötig. Ob die ganze Datei gültig ist, prüft die XTF Validierung.
  - `Unsupported`: Das Wurzelelement ist kein INTERLIS-2.4-Transfer, etwa bei einer INTERLIS-2.3-Datei. Daraus wird nichts gelesen.
  - `Unreadable`: kein lesbares XML, oder ein Objekt jenseits der Grenzen.
- `StatusMessage`: Eine lokalisierte Status-Nachricht vom Typ `LocalizedText`, welche den gelesenen Wert nennt oder erklärt, warum keiner gelesen wurde: bei mehreren Treffern mit den gefundenen Werten, bei unlesbarem XML mit Zeile und Position, soweit der Parser sie kennt.

Kein Wert ist kein Fehler des Schrittes: Ob der Scope Pflicht ist, entscheidet die Pipeline mit einer Condition. Für `FileState` empfiehlt sich die Form `!= 'Readable'`. Sie deckt beide Fehlerzustände ab, und ein Tippfehler im Vergleichswert lässt die Condition immer greifen, statt sie still wirkungslos zu machen.

## Beispiel

```yaml
processes:
  - id: xtf_metadata
    implementation: Geopilot.Pipeline.Processes.XtfMetadata.XtfMetadataExtractorProcess
    default_config:
      scope:
        path: "DMAV_HoheitsgrenzenAV_V1_0:Gemeinde/DMAV_HoheitsgrenzenAV_V1_0:BFSNummer"
        fetch: CurrentlyValid
  # xtf_matcher und xtf_validator wie in den übrigen Pipelines
pipelines:
  - id: dmav_validation
    display_name:
      de: DMAV-Validierung
      en: DMAV validation
    steps:
      # xtf_matching wie in den übrigen Pipelines
      - id: metadata
        display_name:
          de: Metadaten
          en: Metadata
        process_id: xtf_metadata
        conditions:
          pre:
            fail_conditions:
              - id: exactly-one-xtf-required
                expression: "Length([xtf_matching.XtfFiles]) != 1"
                message:
                  de: "Es muss genau eine XTF-Datei hochgeladen werden."
                  en: "Exactly one XTF file must be uploaded."
          post:
            fail_conditions:
              - id: xtf-not-readable
                expression: "[metadata.FileState] != 'Readable'"
                message:
                  de: "Die Transferdatei ist kein lesbarer INTERLIS-2.4-Transfer."
                  en: "The transfer file is not a readable INTERLIS 2.4 transfer."
              - id: scope-required
                expression: "[metadata.FileState] == 'Readable' && [metadata.Scope] == null"
                message:
                  de: "Die BFS-Nummer der Gemeinde liess sich nicht aus der Lieferung lesen."
                  en: "The BFS number of the municipality could not be read from the delivery."
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
        output_actions:
          - property: StatusMessage
            actions:
              - StatusMessage
      - id: validation
        display_name:
          de: XTF Validierung
          en: XTF Validation
        process_id: xtf_validator
        input:
          transferFile: "${step_output(xtf_matching.XtfFiles)}"
          scope: "${step_output(metadata.Scope)}"
```
