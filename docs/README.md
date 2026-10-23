# Dokumentation

Dokumentation zu geopilot für Betreiber, Pipeline-Autoren und Plugin-Entwickler. Die Anleitung zum Einrichten der Entwicklungsumgebung befindet sich im [README](../README.md) im Wurzelverzeichnis.

## Betrieb

- [Ausführungsprotokoll](Ausfuehrungsprotokoll.md): Der dauerhafte Prüfnachweis pro Verarbeitungsjob. Was aufgezeichnet wird, der Definitions-Snapshot, die Abfrage über API und SQL, Aufbewahrung und Datenschutz.
- [Maschinelle Anlieferung](MaschinelleAnlieferung.md): Die Schnittstelle für automatisierte Datenlieferungen. Authentifizierung und Registrierung der Maschinen-Clients, Ablauf und Endpunkte, die beiden Formen je nach Ablage der Uploads, Statusmodell, Downloads und Lebensdauer eines Anlieferungsversuchs.
- [Referenzdaten aktualisieren](Referenzdaten.md): Wie die Referenzdaten einer Validierung zum Wrapper gelangen und aktuell bleiben, mit dem Image `refdata-update` neben geopilot und seinem Einsatz in der lokalen DMAV-Testwelt.
- [Weitergabe betreiben](Weitergabe.md): Wie geopilot abgeschlossene Lieferungen weitergibt. Ablauf, Wiederholungen und Zeitlimit, Verhalten bei Neustart und Ausfall, Stand und Log-Meldungen, Zurücksetzen und was eine Installation unter IIS braucht, damit die Weitergabe pünktlich läuft.

## Pipeline

- [Pipelines](pipeline/Pipelines.md): Grundlagen und Konzepte. Aufbau einer Pipeline, Format der YAML-Definition, Datenfluss zwischen den Schritten, Status-Modell, Konfiguration der Prozessoren, Conditions, Einschränkungen für die Datenlieferung und die Weitergabe nach dem Abschluss.
- [Prozessoren](pipeline/Pipelines.md#prozesse): Referenz zu jedem Prozessor, welcher mit geopilot ausgeliefert wird, je mit Implementierungsname, Konfiguration, Inputs und Outputs.
- [Kanäle](pipeline/Pipelines.md#kanäle): Referenz zu jedem Kanal, welcher eine Lieferung nach dem Abschluss weitergibt, je mit Implementierungsname, Konfiguration und Ablauf.
- [Plugin System](pipeline/pluginSystem.md): Wie eigene Prozessoren als Plugin entwickelt und in geopilot eingebunden werden. Baut auf [Pipelines](pipeline/Pipelines.md) auf.

Die Beispiel-Definitionen, welche mit geopilot ausgeliefert werden, liegen unter [`src/Geopilot.Api/PipelineDefinitions/`](../src/Geopilot.Api/PipelineDefinitions/).
