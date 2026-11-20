# Pflicht-Claims im Access Token konfigurieren (#1225)

Review: required. Sicherheitsrelevant: Die Änderung entscheidet, welche Tokens geopilot annimmt.

## Umsetzung

Der Betreiber legt in den Appsettings Paare aus Claim und Wert fest, etwa `tid` bei Entra oder `urn:zitadel:iam:user:resourceowner:id` bei ZITADEL. Ohne Eintrag bleibt alles wie heute. Ein leerer Claim oder Wert lässt den Start scheitern.

geopilot prüft die Paare bei der Authentifizierung: bei JWT im Token, bei Opaque-Tokens in der Antwort der Introspection. Jedes Paar muss passen, und der Wert muss genau stimmen. Sonst scheitert die Authentifizierung mit 401, und geopilot legt keinen Benutzer an.

Die Prüfung gilt für Personen und Maschinen-Clients und auch an anonym erreichbaren Endpunkten. Dort gilt ein abgelehntes Token als fehlend.

## Nicht im Umfang

- Mehrere erlaubte Werte für denselben Claim.

## Prüfung

- Tests für JWT und Opaque: Der richtige Wert wird angenommen. Ein fehlender Claim und ein anderer Wert ergeben 401, und es entsteht kein Benutzer.
- Ohne Eintrag laufen die bestehenden Tests unverändert.
