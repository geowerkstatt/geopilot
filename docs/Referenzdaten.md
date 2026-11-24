# Referenzdaten aktualisieren

Eine Validierung mit Referenzdaten (etwa DMAV mit `refMapping`) liest die Referenzdaten nicht aus geopilot. ilivalidator holt sie im ilitools-wrapper selbst aus einem INTERLIS-Repository, das in den `modelDirs` steht: `ilidata.xml` nennt die Datensätze, das Mapping ordnet sie einem Scope zu, und die Dateien liegen unter den Pfaden, die `ilidata.xml` angibt. geopilot übergibt nur die Id des Mappings und den Scope aus der [Lieferung](../GLOSSARY.md#lieferung); nennt die Validierung das Mapping mit `refMappingFile` als Datei oder wird es im Admin-Portal gepflegt (siehe unten), schickt geopilot statt der Id das Mapping selbst mit. Die Referenzdaten kommen in allen Fällen aus dem Repository.

Damit die Prüfungen gegen einen aktuellen Stand laufen, müssen diese Dateien regelmässig neu bezogen werden. Das erledigt das Image [`ghcr.io/geowerkstatt/refdata-update`](https://github.com/geowerkstatt/refdata-update) neben geopilot: Es bezieht die konfigurierten Quellen beim Start und danach nach Zeitplan, ersetzt eine Datei erst, wenn der neue Stand vollständig vorliegt, und meldet sich als `unhealthy`, wenn das länger nicht mehr gelungen ist. Einstellungen, Format der Quellen, Verhalten bei Fehlern und die Grenze durch den Cache des Wrappers beschreibt das README dort.

Das Refdata-Repository gehört in die `modelDirs` der Validierung. Vorgesehen ist, dass es der ilitools-wrapper selbst anbietet: als Verzeichnis, in das `refdata-update` schreibt und das die Validierung als `%REPOSITORIES/<id>` nennt (siehe [XTF-Validierung](pipeline/Prozessoren/xtf-validierung.md)). ilivalidator liest es dann direkt, ohne Download und ohne Cache, und ein neuer Stand gilt ab dem nächsten Lauf. Liegt es stattdessen unter einer URL, muss der Wrapper diese erreichen, und es gilt die Grenze durch seinen Cache aus dem README von `refdata-update`.

Jede Datei des Repositorys hat genau eine Herkunft: Entweder kommt sie mit dem Repository, oder `refdata-update` holt sie. Eine Datei, die in den Quellen steht, wird deshalb nicht mit dem Repository ausgeliefert oder eingecheckt; der Job würde sie überschreiben.

## Eine neue Quelle

Ein Eintrag in den Quellen von `refdata-update` allein genügt nicht, ilivalidator erreicht die Datei sonst nie. Dazu gehören:

1. **Ein Eintrag in der `ilidata.xml` des Repositorys** mit einer Id und dem Pfad, den die Quelle als `destination` nennt. Das Mapping nennt nur die Id, erst `ilidata.xml` führt von dort zur Datei. Ein Ziel ohne diesen Eintrag lehnt `refdata-update` ab Version 1.1 ab: Der Lauf gilt als gescheitert, und hält das an, meldet sich der Container nach `REFDATA_UPDATE_MAX_AGE_HOURS` als `unhealthy`.
2. **Das Modell der Daten**, auffindbar über die `modelDirs`: aus einem öffentlichen Repository, oder als `.ili` im Repository samt Eintrag in dessen `ilimodels.xml`. Die Daten selbst stehen nicht in `ilimodels.xml`.
3. **Ein Eintrag im Mapping** für jeden Scope, der die Daten braucht, mit `ilidata:<Id>`; mit `RefMapping:Enabled` im Admin-Portal.
4. **Keine eingecheckte Kopie** der Datei, siehe oben.

## Zuordnung im Admin-Portal

Mit `RefMapping:Enabled` pflegen Administratoren das Mapping im Admin-Portal unter _Referenzdaten_, statt es als Datei oder im Repository auszuliefern. Der Schalter ist in `appsettings.json` aus; ist er aus, fehlt die Seite im Portal, und die API unter `api/v1/refmapping` gibt es nicht.

Ein Eintrag nennt einen Scope, etwa die BFS-Nummer einer Gemeinde, die Referenzdaten als Liste und optional einen Kommentar. Pro Scope gibt es höchstens einen Eintrag. Eine Referenz hat die Form `ilidata:<Id>` mit der Id aus der `ilidata.xml` des Repositorys; URLs und Pfade nimmt das Portal nicht an, weil `ilivalidator` holt, was das Mapping nennt. Ob die Id im Repository existiert, prüft das Portal nicht: Eine Referenz, die dort fehlt, fällt erst bei der Validierung auf.

Was im Portal steht, gilt für jeden Validierungsschritt, der `refMappingFromPortal: true` setzt; alle anderen Schritte validieren wie bisher. Wie die Validierung damit umgeht, beschreibt [XTF-Validierung](pipeline/Prozessoren/xtf-validierung.md#zuordnung-aus-dem-admin-portal). Welche Einträge ein Job bekam, steht im [Ausführungsprotokoll](Ausfuehrungsprotokoll.md#die-bindungen).

### Umstieg von der Datei

Bestehende Mappings übernimmt geopilot nicht. Der Schalter allein ändert keine Validierung, umgestellt wird pro Schritt. Wer umsteigt:

1. schaltet `RefMapping:Enabled` ein und erfasst die Einträge des bisherigen Mappings im Portal, die Referenzen eines Scopes zusammen in einem Eintrag,
2. setzt in den Validierungsschritten, die das Portal verwenden sollen, `refMappingFromPortal: true` und entfernt dort `refMapping` und `refMappingFile`, auch aus der Basis-Konfiguration der Appsettings; ein Schritt, der beides nennt, scheitert,
3. löscht die Datei aus dem Ressourcen-Verzeichnis, sobald keine Definition sie mehr nennt.

Schritte ohne `refMappingFromPortal` laufen unverändert weiter, mit und ohne Schalter. Wer den Schalter wieder ausschaltet, entfernt ebenfalls `refMappingFromPortal: true` aus allen Schritten, sonst scheitern sie.

## Lokal

Das lokale `docker-compose.yml` enthält den Dienst `refdata-update` im Profil `dmav`, zusammen mit der [DMAV-Testwelt](../TestData/Dmav/README.md), und die Testwelt ist gleich aufgebaut wie ein Hosting: Die Dateien aus [`config/refdata-sources.yaml`](../config/refdata-sources.yaml) sind nicht eingecheckt, der Dienst holt sie beim Start nach `TestData/Dmav/repository/`, das der Wrapper als `%REPOSITORIES/dmav@0.1.1` anbietet. Ohne diesen Lauf fehlen sie, und die DMAV-Validierung scheitert. Das Mapping kommt lokal aus dem Admin-Portal: `RefMapping:Enabled` ist in `appsettings.Development.json` gesetzt, der Seed legt den Eintrag der Testwelt an und die DMAV-Pipeline in `basicPipeline_02.yaml` verwendet ihn mit `refMappingFromPortal: true`.

```bash
docker compose --profile dmav up -d
```

`docker compose logs refdata-update` zeigt, welche Dateien der Dienst geholt hat. `.gitignore` schliesst jede dieser Dateien aus; eine neue Quelle braucht dort also zusätzlich eine Zeile.

HFP1 bezieht der Dienst bewusst nicht: swisstopo liefert LFP1 und HFP1 bei jedem Export mit derselben Basket-Id, und ilivalidator lädt dann nicht beide ("BID ... already exists"). Bis das mit der Quelle geklärt ist (#1030), bleibt die HFP1-Datei der Testwelt stehen.
