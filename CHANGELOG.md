# Changelog

## [Unreleased]

### Changed

- The navigation entries in the header, the sidebars and the footer are real links now, so browser features like opening a page in a new tab (Ctrl+click or middle-click) or copying the link address work on them.
- The XTF validation runs `ilivalidator` through the [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper) (v1.0.7 or newer) instead of the interlis-check-service, and is configured accordingly: `validationProfile` is the dataset id of the profile as indexed in one of the new `modelDirs`, the ordered INTERLIS model repositories the models are resolved from, given as one semicolon separated value of `http(s)` URLs and the placeholder `%ITF_DIR`. Since the repositories decide what a validation checks against, `modelDirs` belongs in the appsettings base configuration, where no pipeline definition and no single step can override it. Deployments whose validation profile lived in a repository mounted into the interlis-check-service must publish that repository and list it in `modelDirs`; setting `modelDirs` replaces the default repositories of the tool, so `https://models.interlis.ch/` has to be listed explicitly if it should still apply. The parameters `checkServiceBaseUrl` and `pollInterval` no longer exist and are ignored where they are still configured. A status message is now produced by geopilot in all supported languages instead of being passed through from the service. The interlis-check-service applied its own bundled profile to every validation, including runs that configured no profile, and that profile told the validator to assume all objects are accessible; the validation step keeps doing so through its new `allObjectsAccessible` parameter, which defaults to true. A pipeline that validates parts of a dataset and has to allow references to objects outside the delivered file sets it to false. A `validationProfile` that none of the `modelDirs` indexes now fails the validation step with an error instead of reporting the delivered data as invalid, because the profile comes from the pipeline definition and the data was never checked.
- The input of the XTF validation is named `transferFile` instead of `iliFile`. The step validates a transfer file, while `.ili` is the extension of INTERLIS model files. Existing pipeline definitions must rename that input key; one that still says `iliFile` is rejected when the application starts.
- The XTF validation error tree groups and filters errors by typed fields, configured per step with the field names `errorType`, `model`, `topic` and `class` (the built-in pipelines use all four). The tree shows the number of entries per group and displays the error-category titles and the field labels in the active language.
- Pipeline step `input` is now a map from process parameter name to value, replacing the previous list of `from`, `take` and `as` entries. A value is a literal, a `${step_output(stepId.outputName)}` reference, or a YAML list of those. Existing pipeline definitions must be updated to the new form.
- A pipeline step no longer declares which outputs it exposes: every public property of a process result is available to later steps by its PascalCase name (for example `${step_output(matcher.XtfFiles)}`). The `output:` block with `take`/`as` is replaced by an optional `output_actions:` list that only tags a result property with actions (`Download`, `Delivery`, `StatusMessage`, `Visualization`), and outputs can no longer be renamed. Existing pipeline definitions must be updated to the new form.
- A process run method receives a file collection as `IPipelineFile[]`, which can be wired from any input source (`${upload()}`, a step output, `${file(...)}`, or a combination). The `IPipelineFileList` type has been removed from `GeoWerkstatt.Geopilot.PipelineCore`; file collections are plain `IPipelineFile[]` (or `IReadOnlyList<IPipelineFile>`), and the file filters (`WithExtensions`, `WithMatchingName`) are extension methods on `IEnumerable<IPipelineFile>`.
- Data-derived delivery gating moved from the pipeline to the step: a step ends in a `DeliveryRestriction` state through a post `restrict_delivery_conditions` list, which aggregates into the job state and blocks delivery, shown with its own status in the delivery view while the delivery step is shown as skipped and carries the reason. This replaces the pipeline-level `delivery_restrictions`; existing pipeline definitions must move a blocking condition to `restrict_delivery_conditions` on the step that produces the value. The built-in XTF validation pipelines restrict delivery when the validation was not successful, and a failed or cancelled run keeps its own message on the processing step.
- A step's condition messages (why it failed, was skipped, restricted delivery, or ended with a warning) are now shown as the step's tooltip in the delivery view, separate from the process's own status message, which continues to be shown inline.
- Post-conditions (`fail_conditions`, `warn_conditions`, `restrict_delivery_conditions`) may reference the current step's own output and earlier steps, but no longer a later step: a reference to a step that runs afterwards is now rejected when the pipeline definition is loaded (it was previously accepted but had no value at runtime). Pre-conditions remain restricted to earlier steps.
- A configured pipeline-process parameter whose value cannot be converted to the parameter type now fails startup validation and process creation instead of being silently ignored. Enum-valued parameters accept their member names case-insensitively, also inside lists. Pipeline definitions should be re-checked on upgrade, since a previously ignored typo now prevents the application from starting.
- The name of a mandate is localized (was `string` is now `LocalizedText`). The name can be defined for the different languages in the mandate administration.
- The title shown on the delivery page is configured per environment in `client-settings.json` under `application.localTitle` (a language-code to text map) instead of a fixed built-in translation, so each deployment can present a title tailored to the customer. It is shown in the active language, falls back to another configured language, and is hidden only when no title is configured at all.
- The application name shown in the header is resolved from `application.localName` in `client-settings.json` with cross-language fallback; the non-localized `application.name` default has been removed, so a deployment that configured only `name` must move that value into `localName`.

### Added

- A pipeline process can receive a file the deployment ships as a configuration parameter: a constructor parameter of type `IPipelineFile` is configured with a path relative to `Storage:ResourcesDirectory`, the same root a `${file(path)}` input reference uses. A path that leaves that root or names no existing file fails startup validation. Unlike an input, such a parameter can be declared in the appsettings base configuration, where no pipeline definition can override it.
- The XTF validation can take its INTERLIS model repository as a ZIP archive through the new `modelRepository` parameter, so a repository that is not published can be used: the ilitools-wrapper unpacks it next to the transfer file, which `modelDirs` reaches through the placeholder `%ITF_DIR`. The archive is used verbatim and can define models, carry a validation profile and point at further repositories, so it belongs to the same operator configuration as `modelDirs`.
- A plugin test project can build a pipeline from its own definition file with `PipelineFactory.Builder()` in `GeoWerkstatt.Geopilot.Pipeline`, run it and inspect the result, instead of assembling steps and their inputs by hand. A test therefore covers the definition it ships: the same file, the same `${...}` input expressions and the same built-in processes that the host uses at run time.
- Validation errors can be explored visually in the delivery view: when a validation step fails, its errors are shown on an interactive map and in an error tree.
- `Visualization` output action in the `GeoWerkstatt.Geopilot.Pipeline` runtime: a pipeline step can tag an output as a self-describing visualization config (a `{ type, data }` envelope), which the runtime serves to the frontend to render based on its `type`. Enum values in visualization payloads are serialized as camelCase strings.
- A pipeline step `input` value can reference a file shipped with the deployment via `${file(path)}` (relative to the configured `Storage:ResourcesDirectory`), injecting a constant resource such as a template or lookup table into a process without a preceding step.
- A pipeline step `input` value can reference the uploaded delivery files with `${upload()}`, so a pipeline definition can wire the upload to a process parameter explicitly.
- Pipeline processes can use an `IIli2GpkgClient` from `GeoWerkstatt.Geopilot.PipelineCore` to run ili2gpkg operations using an [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper) service.
- An ili2gpkg operation can resolve models from customer specific INTERLIS model repositories and apply a validation profile from one of them: `Ili2GpkgArgs` carries `ModelDirs` (ordered repositories, `http(s)` URLs or the tool placeholders `%XTF_DIR` and `%ILI_FROM_DB`) and `MetaConfig` (`ilidata:<DatasetId>`). Setting `ModelDirs` replaces the default repositories of the tool, so the standard repositories have to be listed explicitly if they should still apply. Requires ilitools-wrapper v1.0.7 or newer.
- The pipeline definition is checked at startup and the application refuses to start on an invalid one: a step `input` that references an output an earlier step does not produce, or whose type cannot be bound to the target process parameter, and an `output_actions` entry that tags a non-existent or type-incompatible result property, are reported with the affected pipeline, step and property instead of only surfacing at run time.
- A step condition expression (`skip_conditions`, `fail_conditions`, `warn_conditions`, `restrict_delivery_conditions`) that references a `stepId.property` which is not a readable property of that step's result type is now rejected when the pipeline definition is loaded, reported with the affected step and property, instead of only misbehaving at run time.
- Mandates can have a localized description. The description is shown to the users when they choose a mandate before processing.
- The documentation of the pipeline definition format, of the processors shipped with geopilot and of the plugin system is now published with the code under [`docs/pipeline/`](docs/pipeline/Pipelines.md).
- Pipeline steps can end in a `Warning` state through a post `warn_conditions` list: the step ran and reported issues but the pipeline continues, shown with a warning icon in the delivery view. A run whose only non-successful steps are warnings is reported as a warning overall, and a warning does not block delivery on its own.
- A plugin test project can check its pipeline definition before running it: `IPipelineFactory.ValidateDefinition()` in `GeoWerkstatt.Geopilot.Pipeline` reports the same problems, in the same message, that the host reports when it refuses to start. Until now that check lived in the host and was unreachable from outside. Implementations of `IPipelineFactory` outside geopilot have to add the new member.

### Removed

- The runtime types a plugin test does not need are no longer part of the public API of `GeoWerkstatt.Geopilot.Pipeline`: `Pipeline`, `PipelineStep` and its builder, `PipelineFile`, `PipelineFileManager`, `ConditionEvaluator`, `IConditionEvaluator`, `ProcessingStateExtensions`, the compiled `InputValue` kinds and the response types of the built-in XTF validation are now internal. Steps are no longer assembled by hand. A test builds its pipeline from the definition file with `PipelineFactory.Builder()` (see Added) and picks the step it executes out of it.
- The `[UploadFiles]` attribute has been removed from the `GeoWerkstatt.Geopilot.PipelineCore` API. A process parameter that receives the uploaded delivery files must now be wired explicitly with `${upload()}` in the pipeline definition (see Added). Pipeline definitions and plugins that relied on the attribute must be updated.
- The built-in ZIP packaging process no longer has a separate uploaded-files parameter or the `includeUploadFiles` configuration; the files to archive are passed through its single `input` parameter.
- The `interlis-check-service` container has been removed. Validation now runs exclusively through the ilitools-wrapper (see Changed), so deployments must drop that service and the `Pipeline__ProcessConfigs__Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess__checkServiceBaseUrl` environment variable from their compose file.
- The `ili2gpkg-worker` container and its shared job directory have been removed. ili2gpkg operations now run exclusively through the [ilitools-wrapper](https://github.com/geowerkstatt/ilitools-wrapper) service configured with `Ilitools:IlitoolsWrapperAddress`. Deployments must drop the `ili2gpkg-worker` service and the `/shared/ili2gpkg` mount from their compose file; pipeline definitions must drop the `jobsDirectory` configuration of processes that used the worker.
- The `Storage:SharedDirectory` setting and the `/shared` volume of the geopilot image have been removed along with the file-drop integration. A `Storage__SharedDirectory` environment variable or `/shared` mount left in a deployment is ignored.

### Fixed

- The error map in fullscreen can be moved with a single finger and zoomed by scrolling without holding Ctrl (⌘ on macOS). Inline the map keeps asking for two fingers and the modifier key so that it does not swallow the page scroll.
- Processing job and upload timestamps are now recorded in UTC, so the cleanup retention windows (job, download and visualization) are honored regardless of the container time zone. Previously, with the image default `TZ=Europe/Zurich`, expired downloads and visualizations lingered up to two hours longer than configured.
- Files can now be selected for upload on iPhone and iPad. When a mandate limits the accepted file types, iOS and iPadOS browsers previously greyed out the matching files (for example `.xtf`) in the native file picker, so a delivery could not be started from those devices.

## v3.0.341 - 2026-06-17

### Added

- Configurable validation pipelines defined in YAML replace the previous fixed INTERLIS validator: each mandate selects a pipeline of ordered processing steps, with multilingual pipeline and step display names shown in the delivery view.
- Pipeline process plugin system: external processing steps can be written against the new public `GeoWerkstatt.Geopilot.PipelineCore` API and loaded via `Pipeline:Plugins`. The pipeline runtime (`GeoWerkstatt.Geopilot.Pipeline`) and its contracts (`GeoWerkstatt.Geopilot.PipelineCore`) are now published as NuGet packages and version-checked for compatibility when a plugin is loaded.
- Pipeline step conditions and delivery restrictions: steps can fail or be skipped based on expressions, and deliveries can be gated per pipeline. Both surface localized messages per step in the delivery view.
- Per-step localized status messages in the processing result.
- Multi-file upload: a single delivery can contain several files, routed to the matching steps by file matchers.
- Upload of ZIP archives, unpacked and processed by the pipeline.
- Optional cloud upload via presigned URLs with Azure Blob Storage support.
- Cloud upload via presigned URLs with Azure Blob Storage support for files exceeding the API request size limit.
- Optional virus scanning with ClamAV for cloud uploads.
- Rate limiting and upload capacity limits for the cloud upload endpoint.
- `LocalizedText` type in the PipelineCore API for multilingual pipeline texts (pipeline and step display names, status messages). Plugins emitting `Dictionary<string, string>` remain supported.
- Support for public mandates.
- Per-mandate option to allow or disallow deliveries.
- Admins can activate and deactivate users.
- Strict Content-Security-Policy with a per-request nonce for the application and STAC Browser.
- `IPipelineFile.GetLocalPathAsync()` and `IPipelineFileManager.CreateWritableCopyAsync(...)` in the PipelineCore API, letting a process hand a file to external tools by path and obtain an owned, writable copy without copying it by hand.
- Users can view and delete their own uploaded deliveries when logged in. The permission to delete deliveries can be configured to be disabled for all users or restricted to a time duration or interval.

### Changed

- Uploaded files are no longer downloaded before the job starts. A file is fetched from the object storage the first time a pipeline step reads it and reused from there, so a job starts without waiting for the whole upload and a file that a matcher filters out is not fetched while the pipeline runs. The uploaded files are kept in the object storage until the job is cleaned up, or until it ends in a state that cannot be delivered; declaring the delivery archives every uploaded file as primary data, including the ones no step read, so a delivered job transfers all of them eventually. `CloudStorage:CleanupAgeHours` must therefore exceed `Processing:JobRetention` plus `Processing:JobTimeout`; the application logs a warning on start if it does not.
- The `Storage:UploadDirectory` setting and the `/uploads` volume are gone. Uploaded files are now held in the object storage and materialized inside the pipeline working directory, which is removed with the job. Remove the setting and the volume mount from your deployment.
- A pipeline process reads a file with `await file.OpenReadAsync(cancellationToken)` instead of `OpenReadFileStream()`, and takes a local path with `await file.GetLocalPathAsync(cancellationToken)` instead of `GetLocalPath()`. Both accept the job's cancellation token, so a cancelled job also cancels a file that is still being fetched. Plugins built against `GeoWerkstatt.Geopilot.PipelineCore` must be rebuilt against the new version.
- File upload and processing are now decoupled: uploading files returns an upload id, and a separate request starts the processing job for that upload.
- Cloud upload (Azure Blob Storage) is now the single upload mechanism; the previous direct multipart upload and the `CloudStorage:Enabled` configuration switch were removed.
- `GeoWerkstatt.Geopilot.Pipeline` 2.0.0 (breaking): upload files are now passed to `IPipeline.Run` (and `IPipelineFactory.CreatePipeline` no longer takes them) instead of being held on the pipeline instance.
- Files produced by a pipeline step are now offered for download as soon as that step finishes, instead of only after the whole pipeline completes. Downloads from completed steps also remain available if a later step fails or the job times out.
- `Pipeline:Plugins` can now be configured via a single comma-separated value (e.g. `Pipeline__Plugins=a.dll,b.dll`) in addition to the existing JSON array form, making it usable as a flat environment variable.
- Pipeline process inputs are now isolated per step: a process that modifies a file it received no longer affects the original, so other steps consuming the same file are unaffected. Stream reads stay cheap; requesting a local path materializes a private copy.

### Fixed

- Plugin compatibility is now verified before a plugin assembly is loaded for execution (plugins built against a higher PipelineCore minor version are rejected), and plugin dependencies are resolved correctly.
- Download outputs could be missing immediately after a pipeline run, and outputs flagged for delivery could be lost during processing.
- The application no longer crashes when a pipeline or step is missing its display name; such definitions are rejected at startup.
- The application can start without needing the permission to install PostgreSQL extensions if PostGIS is already installed.

## v3.0.227 - 2026-01-07

### Added

- Added support for INTERLIS validation profiles.

### Changed

- Changed how the delivery process works, as mandate selection is necessary before starting validation.
- (Potentially Breaking) Restructure application to use access tokens instead of id tokens for authorisation.
- User data is now fetched from the `/userinfo` endpoint of the IDP instead of parsed from token claims.
- Updated to .NET 10.0.

### Fixed

- Fixed visual issues with Header on mobile devices.
- Fixed bug where language preference is reset in certain circumstances.

## v2.0.203 - 2025-05-01

### Added

- Localisation support for configurable markdown content (Impressum, Privacy Policy etc.), which allows to provide different versions for different languages.
- Localisation support for application name.
- Added new optional `PUID` and `PGID` environment variables to avoid permissions issues between the host OS and the container when using shared directories.

### Changed

- File size limit lowered to 100 MB to adhere to hosting provider restrictions (Cloudflare).
- Error messages from HTTP responses are now displayed localized in the delivery view.
- Moved licenses to separate page and improved layout for a better UX.
- Modularized a component of our CI/CD Workflows.
- Updated README.md to work with new dev-cert naming.
- **BREAKING** Removed references to name "ilicheck", replaced with "INTERLIS".
- Adjusted our Hooks to comply more with ESLint rules.

### Fixed

- Fixed permission issues on shared volumes.
- Sorting and filtering now works consistently across all admin tables.
- File extensions of uploaded files are now checked case-insensitive.
- Fixed an issue where autocomplete dropdown items would duplicate under certain conditions.
- Fixed an issue where STAC Browser would crash due to duplicate filenames.
- Stale ID tokens won't cause infinite API calls anymore.

## v2.0.180 - 2025-02-20

### Added
- Added the option to control how attributes for deliveries are requested from the user for each mandate.
    - All deliveries are migrated to match current behaviour.


### Changed
- The code for this application is now available under the AGPL 3.0 licence.
- **BREAKING** The application updated to STAC-Browser version 3.2.0 and changed how /browser requests are proxied.

## v1.1.143 - 2024-09-30

### Added

- Add Cypress test support.
- Added localization.
- Added separate administration area and user navigation menu to switch between delivery, administration and STAC browser.
- Added grid to manage mandates in administration area.
- Added grid to manage organisations in administration area.
- Added grid to manage users in administration area.
- Added local Keycloak server for development.
- Added authentication in Swagger UI.

### Changed

- Renamed DeliveryMandate to Mandate.
- Rename _Abgabe_ to _Lieferung_.
- Refactored delivery overview to use only [MUI](https://mui.com/material-ui/) components.
- Use Typescript for new components.
- STAC browser now opens in a new tab.
- Use react-oidc-context for authentication.
- Use OpenID Connect "sub" claim as user identifier.
- Expanded API health checks.
- Authenticated users are now registed in the database.
- First registered user is granted administrator privileges.
- Updated to .NET 8.0.
- The app now runs on port 8080 inside the docker container.
- Redesigned complete application.
- **BREAKING** Renamed various public files:
  - `info-hilfe.md` -> `info.md`
  - `impressum.md` -> `imprint.md`
  - `datenschutz.md` -> `privacy-policy.md`
  - `nutzungsbedingungen.md` -> `terms-of-use.md`
- **BREAKING** Deleted _banner_ and _quickstart_ features.
- **BREAKING** Merged `application` and `vendor` properties in Client Settings:
  - The application name is always _geopilot_. With `name` the application name can be extended, e.g. to _geopilot Test_.
  - The url has been removed. As an alternative the link to the organisation can be added to the public files e.g. `info.md`.
  - There is only one `logo` which is used for the header.
  - Optionally a separate `faviconDark` can be defined for dark mode of the browser.
- Removed API version from tab name.

## v1.0.93 - 2024-05-14

### Added

- When releasing a GitHub pre-release, the release notes are automatically updated with the corresponding entries from the `CHANGELOG.md` file.
- Show additional delivery properties in STAC browser.

## v1.0.87 - 2024-04-26

### Added

- Add licensing information to the about page.
- Show delivery comment in STAC browser and on admin page.

### Changed

- Sort delivery mandates alphabetically.

### Fixed

- Spatial extent in STAC browser.
