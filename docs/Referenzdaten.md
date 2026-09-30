# Referenzdaten aktualisieren

Eine Validierung mit Referenzdaten (etwa DMAV mit `refMapping`) liest die Referenzdaten nicht aus geopilot. ilivalidator holt sie im ilitools-wrapper selbst aus einem INTERLIS-Repository, das in den `modelDirs` steht: `ilidata.xml` nennt die Datensätze, das Mapping ordnet sie einem Scope zu, und die Dateien liegen unter den Pfaden, die `ilidata.xml` angibt. Damit die Prüfungen gegen einen aktuellen Stand laufen, müssen diese Dateien regelmässig neu bezogen werden.

Das erledigt das Image `ghcr.io/geowerkstatt/geopilot-refdata-update`. Es bezieht die konfigurierten Dateien beim Start des Containers und danach nach Zeitplan, und es ersetzt jede Datei erst, wenn der neue Stand vollständig vorliegt.

## Betrieb

| Einstellung | Bedeutung |
| --- | --- |
| Volume `/refdata` | Wurzel des Refdata-Repositorys, schreibbar. Dasselbe Verzeichnis liefert ein Webserver (etwa `nginx`) an den Wrapper aus. |
| Datei `/config/sources.yaml` | Die Quellen, siehe unten. Nur lesend. |
| `REFDATA_UPDATE_SCHEDULE` | Zeitplan in Cron-Syntax mit fünf Feldern, Standard `0 1 * * *` (täglich um 1 Uhr). |
| `TZ` | Zeitzone des Zeitplans, etwa `Europe/Zurich`. Ohne Angabe gilt UTC. |

`ilidata.xml`, das Mapping und alle Dateien, die nicht in den Quellen stehen, fasst der Job nicht an. Sie legt der Betreiber in `/refdata` ab und pflegt sie selbst.

Das Repository muss unter einer Adresse liegen, die der Wrapper erreicht. Ein Webserver im selben Compose-Netz (etwa `http://refdata/`) genügt, solange der Wrapper in diesem Netz läuft.

## Quellen

Eine YAML-Liste, ein Eintrag pro Quelle. Jede Quelle wird pro Lauf einmal heruntergeladen. Eine einzelne Datei nennt ihr Ziel mit `destination`; aus einem ZIP-Archiv zählt `extract` die Einträge auf, die entpackt werden, jeder mit seinem Ziel:

```yaml
# Eine Datei
- source: https://example.ch/daten/gemeinden.xtf
  destination: dmav_V1_1/refdata/Gemeinden95_2_4.xtf

# Ein Archiv: entry ist der Pfad im Archiv, destination das Ziel
- source: https://data.geo.admin.ch/ch.swisstopo-vd.ortschaftenverzeichnis_plz/ortschaftenverzeichnis_plz/ortschaftenverzeichnis_plz_2056.xtf.zip
  extract:
    - entry: AMTOVZ_INTERLIS24/OfficialIndexOfLocalities_V1_0.xtf
      destination: dmav_V1_1/refdata/OfficialIndexOfLocalities_V1_0.xtf
```

- `destination` ist relativ zu `/refdata` und entspricht dem `<path>` in `ilidata.xml`. Ein Pfad mit führendem `/` oder mit `..` wird abgewiesen.
- Nur `http` und `https` werden bezogen. Eine Quelle nennt entweder `destination` oder `extract`, nicht beides.
- Eine Vorlage mit den Referenzdaten des DMAV-Repositorys, die Bundesdatensätze mit ihren Quellen, liegt in [`docker/refdata-update/sources.example.yaml`](../docker/refdata-update/sources.example.yaml). HFP1 fehlt darin bewusst: swisstopo liefert LFP1 und HFP1 mit derselben Basket-Id, und ilivalidator lädt dann nicht beide ("BID ... already exists"). Bis das mit der Quelle geklärt ist, bleibt die HFP1-Datei des DMAV-Repositorys stehen.

## Verhalten bei Fehlern

Jede Datei wird neben ihrem Ziel heruntergeladen oder entpackt und erst dann über das Ziel umbenannt, wenn sie vollständig ist und nach einem INTERLIS-Transfer aussieht. Scheitert der Bezug, liefert die Quelle etwa eine Fehlerseite oder fehlt ein Eintrag im Archiv, bleibt der letzte Stand liegen, und das Log nennt Ziel und Quelle. Die übrigen Dateien werden trotzdem bezogen. Ein Lauf, der noch nicht fertig ist, wenn der nächste fällig wird, lässt diesen aus.

Die Aktualisierung ist pro Datei atomar, nicht über alle Dateien zusammen: Eine Prüfung, die während eines Laufs startet, kann alte und neue Dateien mischen.

## Grenze: Cache des Wrappers

ili2c hält Dateien aus einem Repository im Cache des Wrappers (Index 24 Stunden, Datensätze wie das Mapping 12 Stunden). Dateien in einem Unterordner, also die Referenzdaten unter `refdata/`, liest ili2c heute nie aus dem Cache ([claeis/ili2c#165](https://github.com/claeis/ili2c/issues/165)), darum prüft jeder Lauf gegen den neusten Stand. Eine neue Id in `ilidata.xml` oder im Mapping findet ilivalidator dagegen erst, wenn der Cache abgelaufen ist. Sobald der Wrapper das Zurücksetzen seines Caches anbietet ([#1043](https://github.com/geowerkstatt/geopilot/issues/1043)), ruft der Job es nach einem Lauf auf.

## Lokal

Das lokale `docker-compose.yml` enthält den Dienst `refdata-update` im Profil `dmav`, zusammen mit der [DMAV-Testwelt](../TestData/Dmav/README.md). Er bezieht die Quellen aus [`config/refdata-sources.yaml`](../config/refdata-sources.yaml) direkt in `TestData/Dmav/repository/` und ersetzt dort die zugeschnittenen Dateien:

```bash
docker compose --profile dmav up -d
```

`docker compose logs refdata-update` zeigt, welche Dateien ersetzt wurden, `git status` dasselbe als Änderung im Arbeitsbaum. Das volle Ortschaftenverzeichnis ist 383 MB gross und gehört nicht ins Repository; `git restore TestData/Dmav/repository` stellt den eingecheckten Stand wieder her.
