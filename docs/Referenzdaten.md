# Referenzdaten aktualisieren

Eine Validierung mit Referenzdaten (etwa DMAV mit `refMapping`) liest die Referenzdaten nicht aus geopilot. ilivalidator holt sie im ilitools-wrapper selbst aus einem INTERLIS-Repository, das in den `modelDirs` steht: `ilidata.xml` nennt die Datensätze, das Mapping ordnet sie einem Scope zu, und die Dateien liegen unter den Pfaden, die `ilidata.xml` angibt. geopilot übergibt nur die Id des Mappings und den Scope aus der Lieferung; nennt die Validierung das Mapping mit `refMappingFile` als Datei, schickt geopilot statt der Id das Mapping selbst mit. Die Referenzdaten kommen in beiden Fällen aus dem Repository.

Damit die Prüfungen gegen einen aktuellen Stand laufen, müssen diese Dateien regelmässig neu bezogen werden. Das erledigt das Image [`ghcr.io/geowerkstatt/refdata-update`](https://github.com/geowerkstatt/refdata-update) neben geopilot: Es bezieht die konfigurierten Quellen beim Start und danach nach Zeitplan, ersetzt eine Datei erst, wenn der neue Stand vollständig vorliegt, und meldet sich als `unhealthy`, wenn das länger nicht mehr gelungen ist. Einstellungen, Format der Quellen, Verhalten bei Fehlern und die Grenze durch den Cache des Wrappers beschreibt das README dort.

Das Refdata-Repository muss unter einer Adresse liegen, die der Wrapper erreicht, und gehört in die `modelDirs` der Validierung.

## Lokal

Das lokale `docker-compose.yml` enthält den Dienst `refdata-update` im Profil `dmav`, zusammen mit der [DMAV-Testwelt](../TestData/Dmav/README.md). Er bezieht die Quellen aus [`config/refdata-sources.yaml`](../config/refdata-sources.yaml) direkt in `TestData/Dmav/repository/` und ersetzt dort die zugeschnittenen Dateien:

```bash
docker compose --profile dmav up -d
```

`docker compose logs refdata-update` zeigt, welche Dateien ersetzt wurden, `git status` dasselbe als Änderung im Arbeitsbaum. Das volle Ortschaftenverzeichnis ist 383 MB gross und gehört nicht ins Repository; `git restore TestData/Dmav/repository` stellt den eingecheckten Stand wieder her.

HFP1 bezieht der Dienst bewusst nicht: swisstopo liefert LFP1 und HFP1 bei jedem Export mit derselben Basket-Id, und ilivalidator lädt dann nicht beide ("BID ... already exists"). Bis das mit der Quelle geklärt ist (#1030), bleibt die HFP1-Datei der Testwelt stehen.
