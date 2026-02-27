# Changelog

## [Unreleased]

### Added

- Optional cloud upload via presigned URLs with Azure Blob Storage support.
- Optional virus scanning with ClamAV for cloud uploads.

### Fixed

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
