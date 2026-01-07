[![CI](https://github.com/GeoWerkstatt/geopilot/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/GeoWerkstatt/geopilot/actions/workflows/ci.yml) [![Release](https://github.com/GeoWerkstatt/geopilot/actions/workflows/release.yml/badge.svg)](https://github.com/GeoWerkstatt/geopilot/actions/workflows/release.yml) [![Latest Release](https://img.shields.io/github/v/release/GeoWerkstatt/geopilot)](https://github.com/GeoWerkstatt/geopilot/releases/latest) [![License](https://img.shields.io/github/license/GeoWerkstatt/geopilot)](https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE)

# geopilot

geopilot ist ein benutzerfreundliches Tool für das Liefern und Validieren von Geodaten. Es ermöglicht das Hochladen von Geodaten in verschiedenen Formaten und überprüft sie auf Einhaltung geltender Standards. Anwender können ihre hochgeladenen und validierten Daten deklarieren um diese für die Weiterverarbeitung bereit zu stellen. Mit geopilot wird der Prozess der Geodatenverarbeitung für eine reibungslose und zuverlässige Datenübermittlung optimiert.

## Einrichten der Entwicklungsumgebung

Folgende Komponenten müssen auf dem Entwicklungsrechner installiert sein:

✔️ Git  
✔️ Docker  
✔️ Visual Studio 2026 (Erweiterungen ASP.NET & web dev, Node.js development, Container dev tools)

Für die Formattierung wird ESLint verwendet. Dazu im Visual Studio unter `Options/Text Editor/Javascript/Linting/General` _Enable ESLint_ auf `true` setzen, resp. im VS Code die _ESLint_-Extension installieren.

### Starten der Applikation (Lokal) 🚀

- Vor dem ersten Start oder bei Änderungen in den Packages muss in _Geopilot.Frontend_ manuell `npm install` ausgeführt werden.

- Damit die Applikation mit HTTPS funktioniert, muss ein lokales dev-cert erstellt werden. Dieses wird durch das npm Script `predev` vor dem Start automatisch erstellt. Sollte dies nicht funktionieren, kann mit folgendem Befehl ein Zertifikat manuell erstellt und vertraut werden: `dotnet dev-certs https --trust`. HTTPS muss verwendet werden, damit die STAC-Urls korrekt funktionieren und so der STAC-Browser wie in einer produktiven Umgebung verwendet werden kann.

- Das Projekt kann mit dem Launch Profile "Development" gestartet werden.

### Starten der Applikation (Docker Compose) 🐳

Das Projekt unterstützt das Starten der Applikation mit Docker Compose, um einer produktiven Umgebung möglichst nahe zu kommen. Um HTTPS zu unterstützen, benötigt es ein vertrautes dev-cert sowie ein Export dessen im PEM-Format. Diese werden im [docker-compose.yml](./docker-compose.yml) korrekt geladen. Setup ist nachfolgend beschrieben. Die Applikation ist danach unter [https://localhost:5173](https://localhost:5173) erreichbar.

```bash
dotnet dev-certs https --trust
dotnet dev-certs https --export-path ".\certs\cert.pem" --no-password --format PEM
docker compose up -d
```

### URLs Entwicklungsumgebung 🔗

| URL                    | Project                                       | Reverse Proxy                                                             |
| ---------------------- | --------------------------------------------- | ------------------------------------------------------------------------- |
| https://localhost:5173 | Geopilot.Frontend                             | `/api` und `/browser` zu https://localhost:7188                           |
| https://localhost:7188 | Geopilot.Api                                  | `/browser` zu http://localhost:8080 (der `/browser`-Prefix wird entfernt) |
| https://localhost:5173 | Geopilot.Api (in docker-compose mit Frontend) | `/browser` zu http://localhost:8080 (der `/browser`-Prefix wird entfernt) |
| http://localhost:8080  | stac-browser (in docker-compose)              | -                                                                         |
| http://localhost:3001  | PgAdmin (in docker-compose)                   | -                                                                         |
| http://localhost:3080  | interlis-check-service (in docker-compose)    | -                                                                         |
| http://localhost:4011  | Keycloak Server Administration                | -                                                                         |

Das Auth-Token wird als Cookie im Frontend gespeichert und über den Reverse Proxy (in `vite.config.js`) ans API zur Authentifizierung weitergegeben.
Der STAC Browser ist über https://localhost:5173/browser erreichbar und das Cookie kann somit auch da zur Authentifizierung verwendet werden.

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

## Authentifizierung

Fürs Login auf geopilot wird ein Identity Provider mit OpenID Connect (OIDC) vorausgesetzt.
Der verwendete OAuth2 Flow ist _Authorization Code Flow with Proof Key for Code Exchange (PKCE)_.

### Token

Zur Authentifizierung aus dem Frontend wird das ID-Token und aus dem Swagger UI das Access-Token verwendet.
Dabei wird geprüft, dass das Token von der angegebenen Authority ausgestellt wurde (`iss` Claim) und für die Client-Id gültig ist (`aud` Claim).
Zusätzlich werden folgende Claims im Token vorausgesetzt: `sub`, `email` und `name`.
Diese werden beispielsweise bei den [OIDC Scopes](https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims) `openid`, `profile` und `email` mitgeliefert.

### Redirect URIs

Als erlaubte Redirect URIs müssen für das Login aus dem Frontend `https://<app-domain>` und aus Swagger UI `https://<app-domain>/swagger/oauth2-redirect.html` angegeben werden.
_([Entwicklungsumgebung](./config/realms/keycloak-geopilot.json): `https://localhost:5173` und `https://localhost:7188/swagger/oauth2-redirect.html`)_

### Swagger UI

Abhängig vom Identity Provider wird die Audience (`aud` Claim) im Access-Token automatisch gesetzt, sofern ein passender Scope verwendet wird.
Der benötigte Scope kann in den Appsettings under `ApiServerScope` gesetzt werden, um diesen im Swagger UI zur Auswahl anzuzeigen.
Ohne diesem Scope wird das Access-Token möglicherweise ohne oder für eine andere Audience ausgestellt.

In der [Entwicklungsumgebung](./config/realms/keycloak-geopilot.json) wird die Audience stattdessen mit einem Keycloak Protocol Mapper festgelegt.

### Appsettings

Folgende Appsettings können definiert werden (Beispiel aus [appsettings.Development.json](./src/Geopilot.Api/appsettings.Development.json) für die Entwicklungsumgebung):

```json5
"Auth": {
    // General auth options
    "Authority": "http://localhost:4011/realms/geopilot", // Token issuer (required)
    "ClientAudience": "geopilot-client", // ID_Token audience (required)
    "ApiAudience": "geopilot-api", // Access_Token audience (required)
    "FullScope": "openid profile email geopilot.api" // Full scope a client application needs to send as to configure access and id tokens correctly

    // Swagger UI auth options
    "ApiOrigin": "https://localhost:7188", // Swagger UI origin (required)
    "AuthorizationUrl": "http://localhost:4011/realms/geopilot/protocol/openid-connect/auth", // OAuth2 login URL
    "TokenUrl": "http://localhost:4011/realms/geopilot/protocol/openid-connect/token", // OAuth2 token URL
    "ApiServerScope": "<custom app scope>"
}
```

Falls die `AuthorizationUrl` und/oder `TokenUrl` nicht definiert sind, wird im Swagger UI die OpenID Konfiguration der Authority (`<authority-url>/.well-known/openid-configuration`) geladen und alle vom Identity Provider unterstützten Flows angezeigt.
