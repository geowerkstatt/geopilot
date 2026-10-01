# DMAV-Testwelt

Lokaler Smoke-Test der Pipeline `dmav_validation` aus `src/Geopilot.Api/PipelineDefinitions/basicPipeline_02.yaml`: DMAV-Validierung mit Zusatzanforderungen, Referenzdaten und GWR-Plugin, mit echten, öffentlichen Daten der Gemeinde Sauge (BFS 449).

## Inhalt

| Pfad | Inhalt | Herkunft |
| --- | --- | --- |
| `repository/` | INTERLIS-Repository mit Profilen, Configs, Modellen, Index und Mapping | [geowerkstatt/DMAV_ilivalidator](https://github.com/geowerkstatt/DMAV_ilivalidator), Release `v0.1.1` (2026-09-10), Ordner `repositories/`, unverändert |
| `repository/dmav_V1_1/official_models/OfficialIndexOfLocalities_V1_0.ili` | Modell des Ortschaftenverzeichnisses | https://models.geo.admin.ch/Swisstopo/OfficialIndexOfLocalities_V1_0.ili, im DMAV-Repository per `.gitignore` ausgeschlossen |
| `repository/dmav_V1_1/refdata/` | die sieben Referenzdatensätze des Mappings, zugeschnitten | DMAV-Repository `v0.1.1`; `OfficialIndexOfLocalities_V1_0.xtf` fehlt dort und stammt von https://data.geo.admin.ch/ch.swisstopo-vd.ortschaftenverzeichnis_plz/ortschaftenverzeichnis_plz/ortschaftenverzeichnis_plz_2056.xtf.zip (Stand 2026-09-01, Quelle: Bundesamt für Landestopografie swisstopo) |
| `deliveries/` | `DMAVTYM_Alles_V1_1_noError.xtf`, `DMAVTYM_Alles_V1_1_withGwrError.xtf` | DMAV-Repository `v0.1.1`, `data/dmav_V1_1/test_data/`, unverändert |

## Zuschnitt der Referenzdaten

Fenster in LV95: E 2582500 bis 2594500, N 1221000 bis 1231000, die Ausdehnung beider Lieferungen plus 1 km, auf 500 m gerundet. Ein Objekt bleibt, wenn eine seiner Koordinaten im Fenster liegt; Objekte ohne Koordinaten bleiben ganz. Kopf und Baskets bleiben.

| Datei | vorher (Bytes) | nachher (Bytes) | Objekte behalten |
| --- | --- | --- | --- |
| `449_FixpunkteKategorie3.xtf` | 287'556 | 259'531 | 119 von 119 |
| `FixpunkteLV_V1_0_HFP1.xtf` | 10'194'546 | 51'319 | 44 von 8968 |
| `FixpunkteLV_V1_0_LFP1.xtf` | 702'859 | 4'636 | 3 von 634 |
| `Gemeinden95_2_4.xtf` | 572'666 | 572'666 | 2139 von 2139 |
| `OfficialIndexOfLocalities_V1_0.xtf` | 382'750'705 | 2'266'256 | 57 von 8047 |
| `fpds2_BE.xtf` | 45'083'931 | 15'140'966 | 32835 von 57772 |
| `hoheitsgrenze-landesvermessung_2056.xtf` | 24'099'896 | 1'161 | 0 von 50699 |

In `fpds2_BE.xtf` trägt nur `FixpunktVersion` Koordinaten; `Fixpunkt`, `FixpunktAktion` und die übrigen Klassen bleiben deshalb ganz, und die Datei bleibt grösser als die anderen. Im Fenster liegt keine Hoheitsgrenze der Landesvermessung, darum enthält `hoheitsgrenze-landesvermessung_2056.xtf` nur noch Kopf und Basket.

Abgenommen am 2026-09-28: Mit vollen und zugeschnittenen Referenzdaten ergeben `noError`, `withGwrError` und `withError` in ilivalidator 1.15.0 dieselben Fehler und Warnungen. Einzig die Zeilennummer einer Meldung, die in die Referenzdaten zeigt, ändert sich, weil der Zuschnitt die Datei mit einem Objekt pro Zeile schreibt.

## Smoke-Test

1. Dienste samt Plugin starten: `docker compose --profile dmav up -d`. Der Dienst `dmav-plugin` legt das GWR-Plugin ins Volume `ilitools-plugins` und beendet sich; `docker compose logs dmav-plugin` zeigt `fetched` oder `present`.
2. Die API mit der Entwicklungs-Konfiguration starten. Sie liest `basicPipeline_02.yaml` und findet das Repository über `http://interlis-models/dmav/`.
3. Im Admin-Portal ein Mandat anlegen: öffentlich, Pipeline "DMAV-Validierung mit Zusatzanforderungen", Dateityp `.xtf`.
4. `deliveries/DMAVTYM_Alles_V1_1_noError.xtf` liefern: alle Schritte erfolgreich, keine Fehler und Warnungen. Der erste Lauf dauert rund 3.5 Minuten, weil das Plugin die GWR-Datenbank (rund 3 GB) ins Volume `ilitools-cache` lädt, danach knapp 2 Minuten. Der Wrapper übernimmt sie nur aus einem erfolgreichen Lauf in den Cache, darum kommt `noError` zuerst.
5. `deliveries/DMAVTYM_Alles_V1_1_withGwrError.xtf` liefern: die Validierung beschränkt die Lieferung, im Fehlerprotokoll stehen 2 x `GWRC02a` ("EGID existiert nicht im GWR") und 1 x `GWRA17`.

Ohne Profil `dmav` fehlt das Plugin, und der Wrapper lehnt den Validierungsschritt mit `Plugin "ilivalid-gwr@1.0.0-20260217.141026-7" is not available` ab.
