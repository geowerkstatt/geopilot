# Ablage

Legt die Dateien einer Lieferung in einem eigenen Ordner ab, aus dem die Systeme des Empfängers sie übernehmen. Die Ablage ist ein Lieferungs-Schritt (`transfers`): Sie läuft, sobald die Lieferung abgeschlossen ist, und bestimmt mit, ob die Lieferung als weitergegeben gilt.

## Implementierung

Eine Ablage muss unter `channels[X].implementation` den Wert `Geopilot.Pipeline.Channels.FileDrop.FileDropChannel` definieren.

## Konfiguration

| Parameter | Herkunft | Bedeutung |
| --- | --- | --- |
| `baseDirectory` | Basis-Konfiguration, unter `Pipeline:ChannelConfigs` | Das Verzeichnis, unter dem alle Ablagen liegen. Setzt die Pipeline-Definition es, startet geopilot nicht. |
| `subfolder` | Pipeline-Definition (`default_config`, in einer Reaktion überschreibbar) oder Mandat (`mandate_config`) | Der Ordner der Lieferungen, ein relativer Pfad unterhalb des Basisverzeichnisses ohne `..`, etwa `kanton-a/av`. Kein Abschnitt darf nur aus Ziffern bestehen, denn so heissen die Ordner der Lieferungen: statt `kanton-be/351` also etwa `kanton-be/bfs-351`. Ebenso darf keiner mit einem Punkt beginnen, denn so heissen die temporären Ordner. Ein anderer Wert hält den Start an oder lässt das Mandat nicht speichern. |

Das Basisverzeichnis gehört in die Basis-Konfiguration, entweder in die Appsettings:

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

Den Unterordner gibt entweder die Pipeline-Definition fest vor, dann landen die Lieferungen aller Mandate mit diesem Satz im selben Ordner. Mehrere Sätze können denselben Kanal verwenden und je ihren eigenen Unterordner setzen:

```yaml
channels:
  - id: ablage
    implementation: Geopilot.Pipeline.Channels.FileDrop.FileDropChannel
    default_config:
      subfolder: allgemein
reaction_sets:
  - id: ablage_kanton_a
    display_name:
      de: Ablage Kanton A
    transfers:
      - id: ablage_kanton_a
        display_name:
          de: Ablage Kanton A
        channel_id: ablage
        channel_config_overwrites:
          subfolder: kanton-a
```

oder der Satz verlangt ihn vom Mandat, und jedes Mandat trägt seinen eigenen ein. Der Kanal setzt den Unterordner dann nicht in seiner Standard-Konfiguration:

```yaml
channels:
  - id: ablage_pro_mandat
    implementation: Geopilot.Pipeline.Channels.FileDrop.FileDropChannel
reaction_sets:
  - id: ablage_pro_mandat
    display_name:
      de: Ablage pro Mandat
    transfers:
      - id: ablage_pro_mandat
        display_name:
          de: Ablage im Ordner des Mandats
        channel_id: ablage_pro_mandat
        mandate_config:
          - key: subfolder
            display_name:
              de: Unterordner der Ablage
              fr: Sous-dossier du dépôt
              it: Sottocartella del deposito
              en: Subfolder of the file drop
```

## Ablauf

- Jede Lieferung bekommt einen eigenen Ordner nach ihrer Id: `<Basisverzeichnis>/<Unterordner>/<Id der Lieferung>/`. Darin liegen nur die gelieferten Dateien, unter ihrem ursprünglichen Namen. Metadaten der Lieferung, etwa wer sie deklariert hat, legt die Ablage nicht ab.
- Die Ablage schreibt zuerst in einen temporären Ordner `.<Id der Lieferung>.<Zufallsteil>.partial` daneben, jeder Versuch in seinen eigenen, und benennt ihn erst am Ende um. Unter dem Namen der Lieferung liegt also nie etwas Halbes, und wer die Ablage liest, kann Ordner mit einem Punkt am Anfang überspringen.
- Besteht der Ordner der Lieferung schon, gilt sie nur dann als abgelegt, wenn er genau ihre Dateien enthält, mit gleichen Namen und Grössen. So endet ein Versuch, der die Dateien abgelegt hat, sich aber nicht mehr zurückmelden konnte. Liegt dort etwas anderes, scheitert der Versuch, etwa wenn nach dem Zurückspielen der Datenbank die Ids von vorne zählen. Die Ablage überschreibt nichts.
- Scheitert ein Versuch, räumt die Ablage ihren temporären Ordner weg, und der nächste Versuch beginnt von vorne. Stirbt die Instanz mitten in einem Versuch, räumt der nächste Versuch derselben Lieferung zuerst auf. War es der letzte, bleibt der temporäre Ordner liegen, bis ein Admin die Reaktion zurücksetzt; der Betrieb darf ihn auch von Hand löschen.
- Das Basisverzeichnis wird nie angelegt. Fehlt es, etwa weil ein Volume nicht eingebunden ist, scheitert der Versuch. Den Unterordner legt die Ablage bei Bedarf an.

## Betrieb

- geopilot braucht Schreibrechte auf das Basisverzeichnis. In einer Installation mit Containern ist das ein zusätzlich eingebundenes Volume, und weil der Container nicht als root läuft, muss es für dessen Benutzer beschreibbar sein.
- Das Basisverzeichnis darf sich weder mit einem Verzeichnis unter `Storage:` noch mit `Upload:Direct:Directory`, `PublicAssetsOverride` oder dem Web-Root überschneiden, sonst startet geopilot nicht. Die Bereinigungen von geopilot löschen, was sie nicht kennen, die Dateien von `PublicAssetsOverride` und dem Web-Root liefert geopilot ohne Anmeldung aus, und wer die Ablage liest, soll die Daten von geopilot nicht sehen.
- Was abgelegt ist, bleibt in der Ablage, auch wenn ein Admin die Lieferung später löscht.
- Die Ablage gibt die Dateien weiter, wie sie geliefert wurden. Ohne Virenprüfung (`ClamAV:Enabled`) erreichen sie die Systeme des Empfängers also ungeprüft; wer an Dritte weitergibt, schaltet sie ein.
- Jede Installation braucht ihr eigenes Basisverzeichnis, auch Test und Produktion. Die Ids der Lieferungen zählen in jeder Installation für sich.
- Wer die Ablage liest, legt darin keine Verknüpfungen (Symlinks, Junctions) an. Findet die Ablage unterhalb des Basisverzeichnisses eine, scheitert der Versuch, damit sie keine Dateien in ein Verzeichnis umgelenkt schreibt. Das Basisverzeichnis selbst darf eine Verknüpfung oder ein eingebundenes Laufwerk sein.
