[![CI](https://github.com/GeoWerkstatt/geopilot/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/GeoWerkstatt/geopilot/actions/workflows/ci.yml) [![Release](https://github.com/GeoWerkstatt/geopilot/actions/workflows/release.yml/badge.svg)](https://github.com/GeoWerkstatt/geopilot/actions/workflows/release.yml) [![Latest Release](https://img.shields.io/github/v/release/GeoWerkstatt/geopilot)](https://github.com/GeoWerkstatt/geopilot/releases/latest) [![License](https://img.shields.io/github/license/GeoWerkstatt/geopilot)](https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE)

# geopilot

geopilot ist ein benutzerfreundliches Tool für das Liefern und Validieren von Geodaten. Es ermöglicht das Hochladen von Geodaten in verschiedenen Formaten und überprüft sie auf Einhaltung geltender Standards. Anwender können ihre hochgeladenen und validierten Daten deklarieren um diese für die Weiterverarbeitung bereit zu stellen. Mit geopilot wird der Prozess der Geodatenverarbeitung für eine reibungslose und zuverlässige Datenübermittlung optimiert.

## Einrichten der Entwicklungsumgebung

Folgende Komponenten müssen auf dem Entwicklungsrechner installiert sein:

✔️ Git  
✔️ Docker  
✔️ Visual Studio 2022 (Erweiterungen ASP.NET & web dev, Node.js development, Container dev tools)

Für die Formattierung wird ESLint verwendet. Dazu im Visual Studio unter `Options/Text Editor/Javascript/Linting/General` _Enable ESLint_ auf `true` setzen, resp. im VS Code die _ESLint_-Extension installieren.

Damit die Launch Settings für _docker-compose_ korrekt geladen werden, mit Rechtsklick auf dem Projekt _Manage Docker Compose Launch Settings_ öffnen, warten bis alle Services geladen sind und dann speichern.

### Starten der Applikation 🚀

Vor dem ersten Start oder bei Änderungen in den Packages muss in _Geopilot.Frontend_ manuell `npm install` ausgeführt werden.

Über _Start_ > _Configure Startup Projects_ > _Common Properties_ > _Startup Projects_ müssen _Multiple starup projects_ definiert werden.
| Project | Action |
|-----------------|-------------------------|
| docker-compose | Start without debugging |
| Geopilot.Api | Start |
| Geopilot.Api.Test | None |
| Geopilot.Frontend | Start |

### URLs Entwicklungsumgebung 🔗

| URL                    | Project                                    | Reverse Proxy                                                             |
| ---------------------- | ------------------------------------------ | ------------------------------------------------------------------------- |
| https://localhost:5173 | Geopilot.Frontend                          | `/api` und `/browser` zu https://localhost:7188                           |
| https://localhost:7188 | Geopilot.Api                               | `/browser` zu http://localhost:8080 (der `/browser`-Prefix wird entfernt) |
| http://localhost:8080  | stac-browser (in docker-compose)           | -                                                                         |
| http://localhost:3001  | PgAdmin (in docker-compose)                | -                                                                         |
| http://localhost:3080  | interlis-check-service (in docker-compose) | -                                                                         |
| http://localhost:4011  | Keycloak Server Administration             | -                                                                         |

Das Auth-Token wird als Cookie im Frontend gespeichert und über den Reverse Proxy (in `vite.config.js`) ans API zur Authentifizierung weitergegeben.
Der STAC Browser ist auch über https://localhost:5173/browser erreichbar und das Cookie kann somit auch da zur Authentifizierung verwendet werden.

### Debugging 🪲

Das Debugging sollte nun sowohl für das Geopilot.Frontend in JavaScript als auch für Geopilot.Api in C# funtkionieren.

PgAdmin kann für eine Analyse der Datenbank verwendet werden und ist unter [localhost:3001](http://localhost:3001/) verfügbar.

## Cypress Tests

Die Cypress Tests können mit `npm run cy` oder `npm run test` gestartet werden. Sie werden zudem automatisch in der CI/CD Pipeline ausgeführt. Das Projekt ist mit [Cypress Cloud](https://cloud.cypress.io/) konfiguriert, wodurch unter anderem die parallele Ausführung der End-to-End (E2E) Tests ermöglicht wird. Testergebnisse und Aufzeichnungen sind ebenfalls direkt in [Cypress Cloud](https://cloud.cypress.io/) einsehbar, was die Identifikation und Behebung möglicher Fehler und Probleme erleichtert. Um die detaillierten Testergebnisse einzusehen und die E2E-Tests des Projekts zu debuggen, kann die [Cypress Dashboard-Seite](https://cloud.cypress.io/projects/bqtbpp/runs) besucht werden.

## Health Check API

Für das Monitoring im produktiven Betrieb steht unter `https://<host>:<port>/health` eine Health Check API zur Verfügung. Anhand der Antwort _Healthy_ (HTTP Status Code 200), resp. _Unhealthy_ (HTTP Status Code 503) kann der Status der Applikation bspw. mit cURL abgefragt werden.

```bash
curl -f https://<host>:<port>/health || exit 1;
```

Der Health Check ist auch im Docker Container integriert und kann ebenfalls über eine Shell abgefragt werden.

```bash
docker inspect --format='{{json .State.Health.Status}}' container_name
```

## Neue Version erstellen

Ein neuer GitHub _Pre-release_ wird bei jeder Änderung auf [main](https://github.com/GeoWerkstatt/geopilot) [automatisch](./.github/workflows/pre-release.yml) erstellt. In diesem Kontext wird auch ein neues Docker Image mit dem Tag _:edge_ erstellt und in die [GitHub Container Registry (ghcr.io)](https://github.com/geowerkstatt/geopilot/pkgs/container/geopilot) gepusht. Der definitve Release erfolgt, indem die Checkbox _Set as the latest release_ eines beliebigen Pre-releases gesetzt wird. In der Folge wird das entsprechende Docker Image in der ghcr.io Registry mit den Tags (bspw.: _:v1.2.3_ und _:latest_) [ergänzt](./.github/workflows/release.yml).
