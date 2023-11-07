[![CI](https://github.com/GeoWerkstatt/geocop/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/GeoWerkstatt/geocop/actions/workflows/ci.yml)

# geocop

geocop ist ein benutzerfreundliches Tool für das Liefern und Validieren von Geodaten. Es ermöglicht das Hochladen von Geodaten in verschiedenen Formaten und überprüft sie auf Einhaltung geltender Standards. Anwender können ihre hochgeladenen und validierten Daten deklarieren um diese für die Weiterverarbeitung bereit zu stellen. Mit geocop wird der Prozess der Geodatenverarbeitung für eine reibungslose und zuverlässige Datenübermittlung optimiert.

## Einrichten der Entwicklungsumgebung

Folgende Komponenten müssen auf dem Entwicklungsrechner installiert sein:

✔️ Git  
✔️ Docker  
✔️ Visual Studio 2022 (Erweiterungen ASP.NET & web dev, Node.js development, Container dev tools)

### Starten der Applikation 🚀

Über _Start_ > _Configure Startup Projects_ > _Common Properties_ > _Startup Projects_ müssen _Multiple starup projects_ definiert werden.
| Project | Action |
|-----------------|-------------------------|
| docker-compose | Start without debugging |
| GeoCop.Api | Start |
| GeoCop.Api.Test | None |
| GeoCop.Frontend | Start |

Mit dem Starten der Applikation wird ein STAC Browser unter [localhost:8080](https://localhost:8080/) gestartet.

### Debugging 🪲

Das Debugging sollte nun sowol für das GeoCop.Frontend in JavaScript als auch für GeoCop.Api in C# funtkionieren.

PgAdmin kann für eine Analyse der Datenbank verwendet werden und ist unter [localhost:3001](http://localhost:3001/) verfügbar.

## Neue Version erstellen

Ein neuer GitHub _Pre-release_ wird bei jeder Änderung auf [main](https://github.com/GeoWerkstatt/geocop) [automatisch](./.github/workflows/pre-release.yml) erstellt. In diesem Kontext wird auch ein neues Docker Image mit dem Tag _:edge_ erstellt und in die [GitHub Container Registry (ghcr.io)](https://github.com/geowerkstatt/geocop/pkgs/container/geocop) gepusht. Der definitve Release erfolgt, indem die Checkbox _Set as the latest release_ eines beliebigen Pre-releases gesetzt wird. In der Folge wird das entsprechende Docker Image in der ghcr.io Registry mit den Tags (bspw.: _:v1.2.3_ und _:latest_) [ergänzt](./.github/workflows/release.yml).
