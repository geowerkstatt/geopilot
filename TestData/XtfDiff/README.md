# XTF-Diff-Testdaten

Zwei Stände einer kleinen DMAV-Lieferung für die Pipeline `xtf_diff` (`basicPipeline_02.yaml`), mit denen sich jeder Fall der Differenzvisualisierung von Auge prüfen lässt. Die Wörter `alt` und `neu` im Dateinamen ordnen die Stände über den Alt/Neu Matcher zu.

| Datei | Inhalt |
| --- | --- |
| `DMAV_Diff_alt.xtf` | alter Stand |
| `DMAV_Diff_neu.xtf` | neuer Stand |

Die Modelle `DMAV_Einzelobjekte_V1_1` und `DMAV_HoheitsgrenzenAV_V1_0` kommen aus der [DMAV-Testwelt](../Dmav/README.md) (`%REPOSITORIES/dmav@0.1.1`). Beide Dateien sind fehlerfrei nach ilivalidator 1.15.0. Der Vergleich mit dem XTF-Diff-Tool 1.1.21 (`--locations`) ergibt 73 Änderungen.

Die OIDs sind sprechend: Das erste Zeichen nennt die Klasse, die letzten zwei die Nummer des Falls. Jeder Fall liegt in einer eigenen Zelle von 80 m bei Sauge, ab E 2587000 / N 1225000 nach Osten und nach Süden. So liegen die Fälle auf der Karte nebeneinander und überdecken sich nicht.

## Fälle

### Einzelobjekte: EONachfuehrung (`a...`), Fläche mit Bögen

| OID | Änderung | Prüfen |
| --- | --- | --- |
| `a...01` | unverändert; die Referenzen auf sie ändern sich, weil Objekte entstehen und verschwinden | Referenzänderungen der Rückrichtung (`entstehender_Messpunkt`, `entstehendes_Einzelobjekt`), je `added` und `deleted` |
| `a...02` | Objekt gelöscht, Perimeter mit Bogen | gestrichelte Fläche, Bogen als Polygon angenähert |
| `a...03` | Objekt hinzugefügt | gepunktete Fläche |
| `a...04` | `Identifikator` 202110004 → 202110004a, `Beschreibung` mit eingefügten Wörtern, `GueltigerEintrag`, Perimeter mit Bogen umgeformt | Markierung nur des `a`; Markierung der eingefügten Wörter; Wert `CURVEPOLYGON`, Karte `POLYGON`; alt blass, neu kräftig |
| `a...05` | ohne Perimeter, nur `Beschreibung` geändert | nur in der Tabelle, kein Kartenknopf |
| `a...06` | Perimeter ergänzt | Geometrie `added` an einem geänderten Objekt |
| `a...07` | Perimeter entfernt | Geometrie `deleted` an einem geänderten Objekt |
| `a...08` | Loch ergänzt | Polygon mit Innenring |

### Einzelobjekte: Messpunkt (`b...`), Punkt

| OID | Änderung | Prüfen |
| --- | --- | --- |
| `b...01` | um 7 m verschoben, `Lagegenauigkeit` 0.040 → 0.020 | Verbindungslinie alt → neu |
| `b...02` | hinzugefügt | gepunkteter Punkt |
| `b...03` | gelöscht | gestrichelter Punkt |
| `b...04` | `Nummer` 8872640004 → 8872640070 | nur Attribut, Punkt am Ort |
| `b...05` | Höhe ergänzt: drei Attribute `added` | drei Zeilen mit leerem alten Wert |
| `b...06` | `Nummer` entfernt, `IstExaktDefiniert` true → false | Attribut `deleted` neben `changed` |
| `b...07` | `Entstehung` von `a...01` auf `a...03` | Referenz `changed` |
| `b...08` | `Entstehung` ergänzt | Referenz `added` |
| `b...09` | `Entstehung` entfernt | Referenz `deleted` |
| `b...10` | um 5 cm verschoben | winzige Verbindungslinie, alt und neu fast deckungsgleich |
| `b...11` | um rund 410 m verschoben | lange Verbindungslinie, Zoom auf beide Stände |

### Einzelobjekte: Einzelobjekt (`c...`), Geometrien in Strukturen

Alle Geometrien des Einzelobjekts liegen in BAGs von Strukturen (`Flaechenelement`, `Linienelement`, `Punktelement`). Das Tool meldet solche BAGs als `attribute` mit dem ganzen BAG als JSON und ohne Lage: Kein Einzelobjekt erscheint auf der Karte, auch kein hinzugefügtes oder gelöschtes.

| OID | Änderung | Prüfen |
| --- | --- | --- |
| `c...01` | Fläche umgeformt | JSON-Wert mit WKT, Markierung der geänderten Koordinaten |
| `c...02` | Linie verlängert | eingefügte Koordinaten markiert |
| `c...03` | Punkt verschoben, `SymbolOri` geändert | |
| `c...04` | Fläche, Linie und Punkt, nur die Linie geändert | |
| `c...05` | zweites `Punktelement` ergänzt | |
| `c...06` | eines von zwei `Linienelement` entfernt | |
| `c...07` | `Einzelobjektart`, `Objektnummer` geändert, `Objektname` und `EGID` ergänzt | gemischte Änderungstypen an einem Objekt, `é` |
| `c...08` | `EGID` entfernt, `Objektstatus`, `Qualitaetsstandard` geändert | |
| `c...09` | hinzugefügt | nur Tabelle |
| `c...10` | gelöscht | nur Tabelle |
| `c...11` | sechs lange `Objektname`, einer geändert | langer Wert, "mehr anzeigen" |
| `c...12` | `Brücke über die Aare` → `Saane` | Umlaute, Markierung innerhalb des Worts |
| `c...13` | `Entstehung` von `a...01` auf `a...03` | Referenz `changed` |
| `c...14` | Fläche entfernt, Punkt ergänzt | `deleted` und `added` an einem Objekt |
| `c...15` | `Symbolposition` in der Fläche verschoben | Geometrie zwei Strukturen tief |
| `c...16` | Linie mit Bogen umgeformt | `CIRCULARSTRING` im Wert |

### HoheitsgrenzenAV (`e...`), mehrere Geometrien pro Objekt

| OID | Änderung | Prüfen |
| --- | --- | --- |
| `e...01` | HHGNachfuehrung unverändert | Referenzen der Rückrichtung, nur Tabelle |
| `e...02` | Gemeinde unverändert | Referenz `Gemeindegrenze` `added` und `deleted`, nur Tabelle |
| `e...11` | Gemeindegrenze: Fläche umgeformt und `Streitig` ergänzt | ein Feature mit Teilen verschiedener Änderungstypen |
| `e...12` | Gemeindegrenze: `Provisorisch` entfernt, `Undefiniert` bleibt | neue Lage als `GEOMETRYCOLLECTION` des verbliebenen Rests |
| `e...13` | Gemeindegrenze: `Streitig` auf eine andere Kante, `Undefiniert` um eine Kante ergänzt | zwei Geometrieänderungen an einem Objekt, je mit eigenem Kartenknopf |
| `e...14` | Gemeindegrenze mit zwei `Streitig`-Kanten hinzugefügt | `GEOMETRYCOLLECTION`, gepunktet |
| `e...15` | Gemeindegrenze mit `Provisorisch` gelöscht | `GEOMETRYCOLLECTION`, gestrichelt |
| `e...21` | Bezirksgrenzabschnitt: Linie umgeformt, `Gueltigkeit` geändert | Linie alt blass, neu kräftig |
| `e...22` | Bezirksgrenzabschnitt hinzugefügt | gepunktete Linie |
| `e...23` | Bezirksgrenzabschnitt gelöscht | gestrichelte Linie |
| `e...26` | Bezirksgrenzabschnitt: Linie ganz ersetzt, ohne gemeinsamen Punkt | alte und neue Linie getrennt, Zoom auf beide |
| `e...27` | Bezirksgrenzabschnitt: neue Linie kreuzt die alte, ohne gemeinsamen Stützpunkt | alt und neu überlagert |
| `e...24` | Kantonsgrenzabschnitt: Bogen umgeformt | Bogen als Linie angenähert |
| `e...25` | Kantonsgrenzabschnitt: `Untergang` ergänzt | Referenz `added` an einem Objekt mit Lage |
