# Pflicht-Claims im Access Token konfigurieren (#1225)

Review: required. Sicherheitsrelevant: Die Änderung entscheidet, welche Tokens geopilot annimmt.

## Umsetzung

Der Betreiber legt unter `Auth:RequiredClaims` eine Liste von Einträgen mit `Name` und `Value` fest, etwa `tid` bei Entra oder `urn:zitadel:iam:user:resourceowner:id` bei ZITADEL. Der Aufbau stellt sicher, dass .NET die Liste korrekt liest, auch wenn ein Claim Sonderzeichen enthält. Ohne Eintrag bleibt alles wie heute. Fehlt bei einem Eintrag `Name` oder `Value`, startet geopilot nicht.

geopilot prüft die Einträge bei der Authentifizierung: bei JWT im Token, bei Opaque-Tokens in der Antwort der Introspection. Jeder Eintrag muss passen, und der Wert muss genau stimmen. Sonst scheitert die Authentifizierung mit 401, geopilot legt keinen Benutzer an und schreibt eine Warnung mit dem Namen des Claims ins Log. Hat der Claim im Token mehrere Werte, passt er nie, auch wenn der Wert darunter ist.

Die Prüfung gilt für Personen und Maschinen-Clients und auch an anonym erreichbaren Endpunkten. Dort gilt ein abgelehntes Token als fehlend.

README und `docs/MaschinelleAnlieferung.md` sagen, dass ein Claim je nach Identity Provider erst mit einem Scope im Token steht. Dies dient rein informativ. Idealerweise sollten die Betreiber mit den Interna ihrer IdP-Lösung vor der Konfiguration im geopilot vertraut sein.

## Nicht im Umfang

- Mehrere erlaubte Werte für denselben Claim.
- Claims mit mehreren Werten, etwa Rollen oder Gruppen. Bei mehreren Werten ist offen, ob einer oder alle passen müssen, und die Identity Provider liefern sie verschieden. Rollen und Gruppen brauchen bei Bedarf eine eigene Lösung.

## Prüfung

- Tests für JWT und Opaque, für eine Person und einen Maschinen-Client: Der richtige Wert wird angenommen. Ein fehlender Claim und ein anderer Wert ergeben 401, und es entsteht kein Benutzer.
- Ein Claim mit mehreren Werten ergibt 401, auch mit dem richtigen darunter. Eine Liste mit nur dem richtigen Wert wird angenommen.
- Ein anonym erreichbarer Endpunkt antwortet mit einem abgelehnten Token gleich wie ohne Token.
- Ein unvollständiger Eintrag verhindert den Start.
- Ohne Eintrag laufen die bestehenden Tests unverändert.
