# Ablage

Legt die Dateien einer Lieferung in einem eigenen Ordner ab, aus dem die Systeme des Empfängers sie übernehmen. Die Ablage ist ein Lieferungs-Schritt (`transfers`): Sie läuft, sobald die Lieferung abgeschlossen ist, und bestimmt mit, ob die Lieferung als weitergegeben gilt.

## Implementierung

Eine Ablage muss unter `delivery_processing[X].transfers[X].implementation` den Wert `Geopilot.Pipeline.Channels.FileDrop.FileDropChannel` definieren.

## Konfiguration

| Parameter | Herkunft | Bedeutung |
| --- | --- | --- |
| `baseDirectory` | Host, unter `Pipeline:ChannelConfigs` | Das Verzeichnis, unter dem alle Ablagen liegen. |
| `subfolder` | Satz (`config`) oder Mandat (`mandate_config`) | Der Ordner der Lieferungen, ein relativer Pfad unterhalb des Basisverzeichnisses ohne `..`, etwa `kanton-a/av`. Ein anderer Wert hält den Start an oder lässt das Mandat nicht speichern. |

Das Basisverzeichnis legt der Betrieb fest, entweder in den Appsettings:

```json
"Pipeline": {
  "ChannelConfigs": {
    "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel": {
      "baseDirectory": "/drop"
    }
  }
}
```

oder als Umgebungsvariable:

```
Pipeline__ChannelConfigs__Geopilot.Pipeline.Channels.FileDrop.FileDropChannel__baseDirectory=/drop
```

Den Unterordner gibt entweder der Satz fest vor, dann landen die Lieferungen aller Mandate mit diesem Satz im selben Ordner:

```yaml
delivery_processing:
  - id: ablage_kanton_a
    transfers:
      - id: ablage_kanton_a
        implementation: Geopilot.Pipeline.Channels.FileDrop.FileDropChannel
        config:
          subfolder: kanton-a
```

oder der Satz verlangt ihn vom Mandat, und jedes Mandat trägt seinen eigenen ein:

```yaml
delivery_processing:
  - id: ablage_pro_mandat
    transfers:
      - id: ablage_pro_mandat
        implementation: Geopilot.Pipeline.Channels.FileDrop.FileDropChannel
        mandate_config:
          - key: subfolder
            display_name:
              de: Unterordner der Ablage
              fr: Sous-dossier du dépôt
              it: Sottocartella del deposito
              en: Subfolder of the file drop
```

## Ablauf

- Jede Lieferung bekommt einen eigenen Ordner nach ihrer Id: `<Basisverzeichnis>/<Unterordner>/<Id der Lieferung>/`. Darin liegen nur die gelieferten Dateien, unter ihrem ursprünglichen Namen.
- Die Ablage schreibt zuerst in einen temporären Ordner `.<Id der Lieferung>.partial` daneben und benennt ihn erst am Ende um. Unter dem Namen der Lieferung liegt also nie etwas Halbes, und wer die Ablage liest, kann Ordner mit einem Punkt am Anfang überspringen.
- Besteht der Ordner der Lieferung schon, gilt sie als abgelegt. Die Ablage überschreibt nichts.
- Scheitert ein Versuch, räumt die Ablage ihren temporären Ordner weg, und der nächste Versuch beginnt von vorne. Stirbt die Instanz mitten in einem Versuch, räumt der nächste zuerst auf.
- Das Basisverzeichnis wird nie angelegt. Fehlt es, etwa weil ein Volume nicht eingebunden ist, scheitert der Versuch. Den Unterordner legt die Ablage bei Bedarf an.

## Betrieb

- geopilot braucht Schreibrechte auf das Basisverzeichnis. In einer Installation mit Containern ist das ein zusätzlich eingebundenes Volume, und weil der Container nicht als root läuft, muss es für dessen Benutzer beschreibbar sein.
- Das Basisverzeichnis darf sich weder mit einem Verzeichnis unter `Storage:` noch mit `Upload:Direct:Directory` überschneiden, sonst startet geopilot nicht. Die Bereinigungen von geopilot löschen, was sie nicht kennen, und wer die Ablage liest, soll die Daten von geopilot nicht sehen.
- Was abgelegt ist, bleibt in der Ablage, auch wenn ein Admin die Lieferung später löscht.
