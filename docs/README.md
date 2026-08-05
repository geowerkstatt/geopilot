# Dokumentation

Dokumentation zu geopilot für Betreiber, Pipeline-Autoren und Plugin-Entwickler. Die Anleitung zum Einrichten der Entwicklungsumgebung befindet sich im [README](../README.md) im Wurzelverzeichnis.

## Pipeline

- [Pipelines](pipeline/Pipelines.md): Grundlagen und Konzepte. Aufbau einer Pipeline, Format der YAML-Definition, Datenfluss zwischen den Schritten, Status-Modell, Konfiguration der Prozessoren, Conditions und Einschränkungen für die Datenlieferung.
- [Prozessoren](pipeline/Pipelines.md#prozesse): Referenz zu jedem Prozessor, welcher mit geopilot ausgeliefert wird, je mit Implementierungsname, Konfiguration, Inputs und Outputs.
- [Plugin System](pipeline/pluginSystem.md): Wie eigene Prozessoren als Plugin entwickelt und in geopilot eingebunden werden. Baut auf [Pipelines](pipeline/Pipelines.md) auf.

Die Beispiel-Definitionen, welche mit geopilot ausgeliefert werden, liegen unter [`src/Geopilot.Api/PipelineDefinitions/`](../src/Geopilot.Api/PipelineDefinitions/).
