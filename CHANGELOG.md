# Changelog

## [Unreleased]

### Added

- Add Cypress test support.
- Added localization.
- Added separate administration area and user navigation menu to switch between delivery, administration and STAC browser.
- Added grid to manage mandates in administration area.
- Added grid to manage organisations in administration area.
- Added grid to manage users in administration area.
- Added local Keycloak server for development.

### Changed

- Renamed DeliveryMandate to Mandate.
- Rename _Abgabe_ to _Lieferung_.
- Refactored delivery overview to use only [MUI](https://mui.com/material-ui/) components.
- Use Typescript for new components.
- STAC browser now opens in a new tab.
- Use react-oidc-context for authentication.

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
