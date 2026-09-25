# Testdaten für den XTF-Metadaten-Extraktor

Der Aufbau stammt aus einem DMAV-Testdatensatz, der Inhalt ist anonymisiert. Übernommen sind der Kopf mit allen Modellen, die Körbe in der Originalreihenfolge, Klassen, Attribute und Verschachtelung, Daten und allgemeine Beschreibungen. Erfunden sind die Gemeinde Musterwil und die übrigen Namen, die BFS-Nummern (7901, 7902 und 7910, von keinem Gebiet belegt), die Kennungen der Objekte und Körbe, die Nachführungsbezirke und Nummern; die Koordinaten sind verschoben. Modellkonform sind die Dateien nicht, denn die meisten Objekte fehlen, Verweise zeigen ins Leere und Geometrien sind gekürzt. Sie taugen für den Extraktor, nicht für Validierungstests.

| Datei | Inhalt |
| --- | --- |
| `dmav_municipality.xtf` | Alle 16 Körbe mit je ihrem kleinsten Objekt. Der Korb `HoheitsgrenzenAV` behält Nachführung, Gemeinde (7901) und Gemeindegrenze, samt dem Verweis der Grenze auf die Gemeinde. Jede Linie mit mehr als vier Punkten ist auf ein geschlossenes Dreieck aus ihren ersten drei Punkten gekürzt. |
| `dmav_truncated.xtf` | `dmav_municipality.xtf`, mitten in der Gemeinde abgebrochen wie ein unvollständiger Upload. |
| `dmav_two_municipalities.xtf` | Derselbe Kopf, nur der Korb `HoheitsgrenzenAV`: die Nachführung, Musterwil und eine Nachbargemeinde (7902) mit derselben Entstehung. |
| `dmav_completed_merger.xtf` | Musterwil geht mit einer genehmigten Fusions-Nachführung unter, eine neue Gemeinde (7910) entsteht mit ihr. Die Nachführungen stehen hinter den Gemeinden, die Verweise zeigen also nach vorne. |
| `dmav_pending_merger.xtf` | Wie `dmav_completed_merger.xtf`, aber die Fusions-Nachführung ist nicht genehmigt (kein `GenehmigtAm`). |
| `dmav_missing_nachfuehrung.xtf` | Nur Musterwil. Die Nachführung, auf die ihre Entstehung verweist, fehlt in der Datei. |
| `with_dtd.xtf` | Von Hand gebaut: eine DTD mit einer Entität, die nicht verarbeitet werden darf. |
| `not_a_transfer.xml` | Von Hand gebaut: wohlgeformtes XML, aber kein Transfer. |

Die Tests verwenden ausserdem `../UploadFiles/iseltwald_gwp_be13_1.xtf` (INTERLIS 2.3) und `../UploadFiles/helloWorld.pdf` (kein XML).
