# Referenzdaten aktualisieren

Eine Validierung mit Referenzdaten (etwa DMAV mit `refMapping`) liest die Referenzdaten nicht aus geopilot. ilivalidator holt sie im ilitools-wrapper selbst aus einem INTERLIS-Repository, das in den `modelDirs` steht: `ilidata.xml` nennt die Datensätze, das Mapping ordnet sie einem Scope zu, und die Dateien liegen unter den Pfaden, die `ilidata.xml` angibt. geopilot übergibt nur die Id des Mappings und den Scope aus der Lieferung; nennt die Validierung das Mapping mit `refMappingFile` als Datei, schickt geopilot statt der Id das Mapping selbst mit. Die Referenzdaten kommen in beiden Fällen aus dem Repository.

Damit die Prüfungen gegen einen aktuellen Stand laufen, müssen diese Dateien regelmässig neu bezogen werden. Das erledigt das Image [`ghcr.io/geowerkstatt/refdata-update`](https://github.com/geowerkstatt/refdata-update) neben geopilot: Es bezieht die konfigurierten Quellen beim Start und danach nach Zeitplan, ersetzt eine Datei erst, wenn der neue Stand vollständig vorliegt, und meldet sich als `unhealthy`, wenn das länger nicht mehr gelungen ist. Einstellungen, Format der Quellen, Verhalten bei Fehlern und die Grenze durch den Cache des Wrappers beschreibt das README dort.

Das Refdata-Repository gehört in die `modelDirs` der Validierung. Vorgesehen ist, dass es der ilitools-wrapper selbst anbietet: als Verzeichnis, in das `refdata-update` schreibt und das die Validierung als `%REPOSITORIES/<id>` nennt (siehe [XTF-Validierung](pipeline/Prozessoren/xtf-validierung.md)). ilivalidator liest es dann direkt, ohne Download und ohne Cache, und ein neuer Stand gilt ab dem nächsten Lauf. Liegt es stattdessen unter einer URL, muss der Wrapper diese erreichen, und es gilt die Grenze durch seinen Cache aus dem README von `refdata-update`.

Jede Datei des Repositorys hat genau eine Herkunft: Entweder kommt sie mit dem Repository, oder `refdata-update` holt sie. Eine Datei, die in den Quellen steht, wird deshalb nicht mit dem Repository ausgeliefert oder eingecheckt; der Job würde sie überschreiben.

## Eine neue Quelle

Ein Eintrag in den Quellen von `refdata-update` allein genügt nicht, ilivalidator erreicht die Datei sonst nie. Dazu gehören:

1. **Ein Eintrag in der `ilidata.xml` des Repositorys** mit einer Id und dem Pfad, den die Quelle als `destination` nennt. Das Mapping nennt nur die Id, erst `ilidata.xml` führt von dort zur Datei. Ein Ziel ohne diesen Eintrag lehnt `refdata-update` ab Version 1.1 ab: Der Lauf gilt als gescheitert, und hält das an, meldet sich der Container nach `REFDATA_UPDATE_MAX_AGE_HOURS` als `unhealthy`.
2. **Das Modell der Daten**, auffindbar über die `modelDirs`: aus einem öffentlichen Repository, oder als `.ili` im Repository samt Eintrag in dessen `ilimodels.xml`. Die Daten selbst stehen nicht in `ilimodels.xml`.
3. **Ein Eintrag im Mapping** für jeden Scope, der die Daten braucht, mit `ilidata:<Id>`.
4. **Keine eingecheckte Kopie** der Datei, siehe oben.

## Lokal

Das lokale `docker-compose.yml` enthält den Dienst `refdata-update` im Profil `dmav`, zusammen mit der [DMAV-Testwelt](../TestData/Dmav/README.md), und die Testwelt ist gleich aufgebaut wie ein Hosting: Die Dateien aus [`config/refdata-sources.yaml`](../config/refdata-sources.yaml) sind nicht eingecheckt, der Dienst holt sie beim Start nach `TestData/Dmav/repository/`, das der Wrapper als `%REPOSITORIES/dmav@0.1.1` anbietet. Ohne diesen Lauf fehlen sie, und die DMAV-Validierung scheitert.

```bash
docker compose --profile dmav up -d
```

`docker compose logs refdata-update` zeigt, welche Dateien der Dienst geholt hat. `.gitignore` schliesst jede dieser Dateien aus; eine neue Quelle braucht dort also zusätzlich eine Zeile.

HFP1 bezieht der Dienst bewusst nicht: swisstopo liefert LFP1 und HFP1 bei jedem Export mit derselben Basket-Id, und ilivalidator lädt dann nicht beide ("BID ... already exists"). Bis das mit der Quelle geklärt ist (#1030), bleibt die HFP1-Datei der Testwelt stehen.
