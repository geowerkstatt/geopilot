# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**geopilot** is a full-stack tool for uploading, validating, and delivering geodata (primarily Swiss INTERLIS/XTF). Validation runs through a configurable YAML pipeline; successful deliveries are exposed through a STAC Browser and can be declared for downstream consumption.

## Tech Stack

- **Backend:** ASP.NET Core on **.NET 10**, EF Core + PostgreSQL/PostGIS (NetTopologySuite), YARP reverse proxy, Swashbuckle/OpenAPI, JWT auth via OIDC (Keycloak in dev), DotNetStac.Api, Docker.DotNet, NCalcAsync, YamlDotNet, optional Azure Blob Storage + ClamAV.
- **Frontend:** React 18 + TypeScript, Vite 7, MUI 5 (+ x-data-grid), react-oidc-context, react-hook-form, i18next, Cypress E2E.
- **Pipeline library:** `Geopilot.PipelineCore` — published as the `GeoWerkstatt.Geopilot.PipelineCore` NuGet package; versioned independently of the API.

## Common commands

All commands run from the repo root unless noted otherwise.

**.NET build / test (whole solution):**
```bash
dotnet restore Geopilot.slnx
dotnet build Geopilot.slnx -c Release /warnaserror
dotnet test Geopilot.slnx -c Release --no-build
```
Tests require a running Postgres (and optionally Azurite/ClamAV). Start them via `docker compose up -d --wait db azurite clamav`. `AssemblyInitialize` wires `TestDatabaseFixture` against that DB.

**Run a single test:**
```bash
dotnet test tests/Geopilot.Api.Test/Geopilot.Api.Test.csproj --filter "FullyQualifiedName~PipelineFactoryTest"
```

**Frontend (in `src/Geopilot.Frontend`):**
```bash
npm install                # required once and after package changes
npm run dev                # Vite dev server on https://localhost:5173
npm run build
npm run lint               # must pass with 0 warnings (--max-warnings 0)
npm run lint:fix
npm run cy                 # open Cypress interactively
npm run test               # cypress run (headless, also used in CI)
```
The `predev` hook runs `aspnetcore-https.js` to provision a dev cert; if it fails run `dotnet dev-certs https --trust` manually.

**EF Core migrations (in `src/Geopilot.Api`):**
```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name>
dotnet tool run dotnet-ef migrations has-pending-model-changes   # CI guard
```
Migrations run automatically on startup (`context.MigrateDatabase()` in `Program.cs`).

**Full docker-compose stack (mirrors production shape):**
```bash
dotnet dev-certs https --trust
dotnet dev-certs https --export-path ".\certs\cert.pem" --no-password --format PEM
docker compose up -d
```
App is then at <https://localhost:5173>. See README for the full URL table (PgAdmin 3001, Keycloak 4011, interlis-check-service 3080, Azurite 10000, ClamAV 3310).

## Architecture

### Solution layout (`Geopilot.slnx`)
- `src/Geopilot.Api` — ASP.NET Core host. Also serves the built SPA and STAC routes.
- `src/Geopilot.Frontend` — Vite/React SPA; proxies `/api`, `/browser`, `/swagger` to the API in dev.
- `src/Geopilot.PipelineCore` — public abstractions for pipeline plugin authors (`IPipelineFile`, `IPipelineFileList`, `IPipelineFileManager`, `IContainerRunner`, `[PipelineProcessRun]`, `[UploadFiles]`). Has `PublicAPI.Shipped.txt` enforced by `Microsoft.CodeAnalysis.PublicApiAnalyzers` — a major bump here forces every plugin to rebuild (see `ValidatePluginCoreCompatibility`).
- `src/Geopilot.ProcessorPluginA` — example external plugin project.
- `tests/Geopilot.Api.Test` — MSTest + Moq test project (integration tests hit the real DB via `TestDatabaseFixture`).

### Request pipeline
`Program.cs` is the single composition root. Key points:
- **Auth:** JWT bearer against `Auth:Authority`, audience `Auth:ApiAudience`. The token can arrive either in the `Authorization` header **or** in the `geopilot.auth` cookie (SPA flow). Default/fallback policy is `GeopilotPolicies.Admin`; individual controllers relax this to `User`.
- **STAC:** `DotNetStac.Api` endpoints are mounted behind `StacRoutingConvention` (admin-only). `/browser/*` is reverse-proxied to the `stac-browser` container and middleware redirects non-admins back to `/`.
- **Uploads & validation:** `ValidationController` → `IValidationService` enqueues an `IPipeline` into `IValidationJobStore`; `ValidationRunner` (hosted service) consumes the channel in parallel, runs pipelines with a `JobTimeout`, and stores `ValidatorResult` back in the store. `ValidationJobCleanupService` removes old jobs after `JobRetention`.
- **Cloud upload path (optional, `CloudStorage:Enabled`):** presigned URLs into Azure Blob Storage (Azurite in dev), `PreflightBackgroundService` + `PreflightChannel` gate uploads, `ClamAvScanService` or `NoOpScanService` handles virus scanning, `CloudCleanupService` bins stale blobs. Rate-limited via the `uploadRateLimit` fixed-window limiter.
- **SPA fallback:** `MapSpaFallback` serves the built frontend; `index.html` is deliberately 404'd by `UseStaticFiles` so `MapSpaFallback` can inject a per-request CSP nonce (`__CSP_NONCE__` placeholder from `vite.config.js`).
- **Dev-only:** on startup, if `Mandates` is empty, `ContextExtensions.SeedTestData()` seeds fake users/orgs/mandates/deliveries via Bogus. Two fixed admins/users match Keycloak subject IDs in `config/realms/keycloak-geopilot.json`.

### Pipeline engine (core architecture — read this before touching validation)
Pipelines are defined in YAML (see `src/Geopilot.Api/PipelineDefinitions/`). The path comes from `Pipeline:Definition` in appsettings **or** the `Pipeline__Definition` env var — the latter overrides. `app.ValidatePipelineConfiguration()` crashes the app on startup if the YAML is invalid, so malformed configs never silently degrade.

Structure:
- `processes:` — named process implementations (type-resolved from the `Geopilot.Api.*` namespace or from plugin assemblies listed under `Pipeline:Plugins`). Each has a `default_config:` block.
- `pipelines:` — ordered `steps:` that reference a `process_id`, optionally with `input`/`output` wiring, `process_config_overwrites`, and pre/post `conditions` (NCalc expressions evaluated by `ConditionEvaluator` — e.g. `Length([xtf_matching.xtf_files]) != 1`).
- `delivery_restrictions:` at the pipeline level decide whether a successful run is allowed to be delivered.

Runtime flow:
1. `PipelineFactory` deserializes the YAML into `PipelineProcessConfig` and per-request builds an `IPipeline` for a job (`jobId` scoped temp directory under `PipelineDirectory`).
2. `PipelineProcessFactory` resolves each step's process. It reflects over `[PipelineProcessRun]`-marked methods, injects constructor-resolved dependencies (`IContainerRunner`, `IPipelineFileManager`, loggers, and the YAML-bound config values), and passes `[UploadFiles]`-marked parameters.
3. Plugin assemblies from `Pipeline:Plugins` are loaded into their own `AssemblyLoadContext` (collectible) and filtered by `ValidatePluginCoreCompatibility` — only plugins referencing a compatible `Geopilot.PipelineCore` major version are kept. The load context is unloaded on dispose.
4. `Pipeline.Run` walks steps sequentially, building a `PipelineContext` that accumulates `StepOutput`s keyed by `stepId.outputName`. Step outputs can be tagged with `Download`, `Delivery`, or `StatusMessage` actions to drive frontend behavior.
5. On failure, a `PipelineRunException` carries structured `PipelineValidationErrors` back to the controller.

Built-in processes live under `src/Geopilot.Api/Pipeline/Process/`:
- `Matcher/XtfMatcher`, `Matcher/FileMatcher` — select uploaded files by extension/model/filename pattern.
- `XtfValidation/XtfValidatorProcess` — polls the external **interlis-check-service** (configured via `Pipeline:ProcessConfigs:…XtfValidatorProcess:checkServiceBaseUrl`).
- `XtfValidatorErrorTree/…` — converts xtf logs into a json error tree.
- `IliToGeoPackage/IliToGeoPackageProcess` — runs `ili2gpkg` in a **one-shot Docker container** via `IContainerRunner` → `DockerContainerRunner` using `Docker.DotNet`. The image (`ghcr.io/geowerkstatt/ili2gpkg:5.1.0`) **must** be in `Pipeline:Docker:AllowedImages` or the run is rejected. Mounting `/var/run/docker.sock` into the geopilot container is what makes this work in docker-compose — note the security caveat in the compose file.
- `ZipPackage/…` — bundles selected files into a delivery archive.

### Persistence
EF Core `Context` (`src/Geopilot.Api/Context.cs`) holds `Users`, `Organisations`, `Mandates`, `Deliveries`, `Assets` with global query filters for soft-deletion (`Deleted` flag on Delivery/Asset). The `…WithIncludes` properties are the canonical way to eager-load related entities. `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` is enabled for spatial columns (mandate extent geometry).

### Frontend structure
- `src/app.tsx` — top-level routes: `/` (delivery), `/admin/*` (admin grids for deliveries/users/mandates/organisations, admin-only), footer pages. Admin routes are conditionally rendered from `useGeopilotAuth().isAdmin`.
- `src/auth/` — react-oidc-context wiring; auth token is set as the `geopilot.auth` cookie so the API and STAC browser share it.
- `src/components/` — shared MUI-based primitives (dropzone, data grid wrapper, form helpers, header).
- `src/pages/delivery` + `deliveryContext.tsx` — delivery wizard state machine.
- Markdown files served from `src/Geopilot.Frontend/devPublic/` (dev) or `/public` volume (prod) provide localizable content (imprint, privacy, about).

## Conventions & gotchas

- **StyleCop** analyzers run on every non-test, non-docker-compose project (`Directory.Build.props`), and CI uses `/warnaserror`. Keep XML doc comments on public members (`GenerateDocumentationFile` is on).
- `InternalsVisibleTo` is set to `$(AssemblyName).Test` globally, so tests can reach internal types without extra attributes.
- **Public API is locked** on `Geopilot.PipelineCore` via `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`. Adding or changing public surface requires updating these files; a major bump cascades to all plugins.
- **Pipeline YAML path must match in two places** when running under docker-compose: the `Pipeline__Definition` env var and the volume mount target in `docker-compose.yml`.
- When adding a process that needs a Docker image, also add the image (exact tag) to `Pipeline:Docker:AllowedImages` in both `appsettings.Development.json` and `docker-compose.yml`. `AllowedImages` is the primary security guard for the mounted docker socket — keep it restrictive.
- CORS `"All"` policy is development-only (used by the STAC browser). Production relies on the same-origin SPA hosting.
- Request body limit is **100 MB** (both `FormOptions.MultipartBodyLengthLimit` and Kestrel `MaxRequestBodySize`). Larger responses are short-circuited to HTTP 413.
- Changelog entries go in `CHANGELOG.md` under `[Unreleased]`; the release workflow copies them into the GitHub release notes.
- Contributions require a `Signed-off-by:` line per the CLA in `CONTRIBUTING.md`.
