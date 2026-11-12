# S3-kompatiblen Object Storage für Uploads unterstützen (#1199)

Review: required. Es kommt eine Abhängigkeit dazu (AWS SDK für S3), es betrifft Uploads, und CI ändert sich.

## Umsetzung

S3 kommt als zweiter Provider neben Azure Blob Storage dazu, Azure bleibt der Standard.

Die Antwort von `POST api/v2/upload` nennt neu die Header für den Upload, und die Clients senden diese. Ein Client, der heute die Header für Azure selbst sendet, funktioniert weiter.

## Nicht im Umfang

Andere Anmeldungen an S3 als mit Access Key, etwa über Rollen des Providers.

## Prüfung

Tests gegen einen S3-Server in CI, dazu die Akzeptanzkriterien des Issues.
