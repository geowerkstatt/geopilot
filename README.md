[![CI](https://github.com/GeoWerkstatt/geocop/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/GeoWerkstatt/geocop/actions/workflows/ci.yml)

# geocop

geocop ist ein benutzerfreundliches Tool für das Liefern und Validieren von Geodaten. Es ermöglicht das Hochladen von Geodaten in verschiedenen Formaten und überprüft sie auf Einhaltung geltender Standards. Anwender können ihre hochgeladenen und validierten Daten deklarieren um diese für die Weiterverarbeitung bereit zu stellen. Mit geocop wird der Prozess der Geodatenverarbeitung für eine reibungslose und zuverlässige Datenübermittlung optimiert.

## Einrichten der Entwicklungsumgebung

Folgende Komponenten müssen auf dem Entwicklungsrechner installiert sein:

✔️ Git  
✔️ Docker  
✔️ Visual Studio 2022 (Erweiterungen ASP.NET & web dev, Node.js development, Container dev tools)  

### Starten der Applikation 🚀

Über *Start* > *Configure Startup Projects* > *Common Properties* > *Startup Projects* müssen *Multiple starup projects* definiert werden.
| Project         | Action                  |
|-----------------|-------------------------|
| docker-compose  | Start without debugging |
| GeoCop.Api      | Start                   |
| GeoCop.Api.Test | None                    |
| GeoCop.Frontend | Start                   |

### Debugging 🪲
Das Debugging  sollte nun sowol für das GeoCop.Frontend in JavaScript als auch für GeoCop.Api in C# funtkionieren.

PgAdmin kann für eine Analyse der Datenbank verwendet werden und ist unter [localhost:3001](http://localhost:3001/) verfügbar.
