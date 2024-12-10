# Changelog

## [Unreleased]

### Changed
- The code for this application is now available under the AGPL 3.0 licence.

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
