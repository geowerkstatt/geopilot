# Lokale Plugin-Konfiguration

Werkzeug, um ein geopilot-Plugin lokal gegen geopilot zu entwickeln, ohne Konfiguration von Hand zu kopieren.

`link-plugin-config.ps1` verlinkt die Entwicklungs-Appsettings eines Plugins als git-ignorierten Symlink in geopilot, sodass geopilot die Plugin-Einstellungen direkt liest. Das Plugin-Repository bleibt die einzige Quelle der Wahrheit.

## Voraussetzungen

- **Symlink-Rechte auf Windows:** einen Symlink zu erstellen braucht Administrator-Rechte oder aktivierten Developer Mode (Einstellungen > Für Entwickler > Entwicklermodus).
- Das Plugin-Repository liegt als **Nachbarordner** neben geopilot (gleiches übergeordnetes Verzeichnis).

## Verwendung

```powershell
./scripts/link-plugin-config.ps1 <Plugin> <Profile> [Name]
```

| Parameter | Bedeutung |
| --- | --- |
| `Plugin` | Ordnername des Plugin-Repositorys neben geopilot. |
| `Profile` | Welche `appsettings.<Profile>.json` im Plugin verlinkt wird (z.B. `Development`). |
| `Name` | Optional. Benennt das Overlay: `appsettings.Local.<Name>.json`. Ohne Angabe schlicht `appsettings.Local.json`. |

Beispiele:

```powershell
# appsettings.Development.json des Plugins als appsettings.Local.json verlinken
./scripts/link-plugin-config.ps1 mein-plugin Development

# benanntes Overlay, damit mehrere Plugins gleichzeitig verlinkt sein koennen
./scripts/link-plugin-config.ps1 mein-plugin Development meinplugin
```

## Funktionsweise

1. Sucht `appsettings.<Profile>.json` im Nachbar-Plugin-Repository (Build-Ausgabe unter `bin`/`obj` wird ignoriert).
2. Erstellt einen **Symlink** `src/Geopilot.Api/appsettings.Local[.<Name>].json`, der auf diese Datei zeigt.
3. Der Link ist über das Muster `**/appsettings.Local*.json` in der `.gitignore` abgedeckt und erscheint deshalb nie in `git status`.
4. geopilot lädt **jede** `appsettings.Local*.json` als optionales Konfig-Overlay (siehe `src/Geopilot.Api/ConfigurationBuilderExtensions.cs`). Die Overlays greifen **nur in der Development-Umgebung** und liegen in der Konfigurationsreihenfolge nach den Appsettings, aber vor den Umgebungsvariablen: sie überschreiben also `appsettings.json` und `appsettings.Development.json`, verlieren aber gegen Umgebungsvariablen und Kommandozeilen-Argumente.

## Mehrere Plugins gleichzeitig

Gib jedem Link einen `Name`. Alle `appsettings.Local.<name>.json` werden zusammen geladen, du kannst also mehrere Plugins gleichzeitig verlinkt haben.

## Warum ein Symlink

- **Eine Quelle der Wahrheit:** die Datei im Plugin-Repository. Kein Kopieren von Hand, kein Auseinanderlaufen über die Zeit.
- **Robust:** ein Symlink löst bei jedem Zugriff über den Pfad auf, funktioniert also weiter nach Editor-Speichern und git-Operationen (Pull, Checkout, Branch-Wechsel), die die Zieldatei ersetzen. Ein Hardlink würde dabei still brechen.

## Gut zu wissen

- **geopilot neu starten** nach dem Verlinken und nach jeder Änderung an der verlinkten Datei. Konfig-Overlays und Plugins werden beim Start gelesen, es gibt kein automatisches Neuladen.
- Findet das Skript **mehrere** `appsettings.<Profile>.json` im Plugin (z.B. Plugin- und Testprojekt), bricht es mit der Trefferliste ab, statt eine beliebige zu verlinken.
- Der Link ist **git-ignoriert**: er erscheint nie in `git status` und ist im Visual-Studio-Solution-Explorer standardmässig ausgeblendet. Das ist so gewollt.
- Halte die `appsettings.<Profile>.json` im Plugin **dünn** (nur plugin-relevante Schlüssel) und nutze Pfade, die aus geopilots Sicht gültig sind.
- **Editor-Tabs können einseitig wirken:** bearbeitest du den Link, aktualisiert sich die Plugin-Datei; bearbeitest du die Plugin-Datei, frischt sich der Tab des Links evtl. erst nach erneutem Öffnen auf. Auf der Platte ist es immer ein und dieselbe Datei.

## Link entfernen

Die Link-Datei löschen, zum Beispiel:

```powershell
Remove-Item src/Geopilot.Api/appsettings.Local.<Name>.json
```

Das Löschen des Symlinks fasst die Plugin-Datei nicht an.

# Maschinelle Anlieferung ausprobieren

Zwei Skripte fahren eine [maschinelle Anlieferung](../docs/MaschinelleAnlieferung.md) von Anfang bis Ende: Token per Client Credentials holen, die Dateien übergeben, den Versuch starten und den Status abfragen, bis er geliefert, abgewiesen oder gescheitert ist. Sie sind zugleich der Startpunkt für einen eigenen Client: jeder Schritt steht mit dem HTTP-Aufruf, den er absetzt, im Skript.

Welches Skript passt, entscheidet die Installation, nicht das Mandat:

| Ablage der Uploads | Skript |
| --- | --- |
| ausserhalb der API (Objektspeicher) | `submit-delivery-upload.ps1`: Upload anmelden, Dateien an die zurückgegebenen URLs, dann `POST api/v1/submission` |
| durch die API (lokales Verzeichnis) | `submit-delivery-files.ps1`: ein `multipart/form-data`-Aufruf an `POST api/v1/submission/files`, die Felder vor den Dateien |

Wer das falsche nimmt, bekommt `400` mit dem Hinweis auf die andere Form.

## Voraussetzungen

- PowerShell 7 (`pwsh`), auf Windows wie auf Linux. Auf Windows zum Beispiel mit `winget install Microsoft.PowerShell`.
- Ein Client mit Client Credentials beim Identity Provider der Installation, in geopilot als Maschinen-Client registriert (Verwaltung, *Maschinen-Clients*), und ein Mandat mit Schlüssel, das er erreicht: öffentlich oder über eine seiner Organisationen.
- Im Dev-Stack ist das der Keycloak-Client `geopilot-api`. Sein Service-Account ist als Maschinen-Client geseedet, das Secret steht in `config/realms/keycloak-geopilot.json`. Die Voreinstellungen der Skripte zeigen auf den Dev-Stack.

## Verwendung

```powershell
$env:GEOPILOT_CLIENT_SECRET = '<Secret des Clients>'
./scripts/submit-delivery-upload.ps1 -MandateKey <Schluessel> -File .\lieferung.xtf -SkipCertificateCheck
```

Der Dev-Stack legt seine Uploads in den Objektspeicher, es läuft dort also `submit-delivery-upload.ps1`. `submit-delivery-files.ps1` braucht eine Installation, die ihre Uploads selbst schreibt (`Upload:Backend=Direct` mit `Upload:Direct:Directory`), und antwortet sonst mit dem `400` auf die falsche Form.

Gegen eine andere Installation `-ApiUrl`, `-TokenUrl`, `-ClientId` und `-ClientSecret` setzen, bei Bedarf `-Scope`; `-Comment`, `-PartialDelivery` und `-PrecursorDeliveryId` je nachdem, was das Mandat verlangt. Mehrere Dateien werden kommagetrennt an `-File` übergeben. `Get-Help ./scripts/submit-delivery-upload.ps1 -Detailed` beschreibt alle Parameter.

Weist die Installation einen Aufruf ab, weil sie gerade nicht mehr annimmt, wartet das Skript und wiederholt ihn bis zu fünfmal: `submit-delivery-upload.ps1` beim Anmelden des Uploads, wo eine zu dichte Folge von Anfragen `429` ergibt, und `submit-delivery-files.ps1` beim Versuch selbst, den eine ausgelastete Installation mit `503` und `Retry-After` abweist. Bricht die Statusabfrage vorübergehend weg, wird auch sie wiederholt, statt den Lauf abzubrechen: die Daten sind zu dem Zeitpunkt längst angenommen. Das Token wird kurz vor Ablauf erneuert, damit auch ein langer Lauf bis zum Ende verfolgt wird.

## Exit-Codes

| Code | Bedeutung |
| --- | --- |
| 0 | geliefert |
| 1 | ein Request wurde abgewiesen; die Meldung des Servers steht in der Ausgabe |
| 2 | die Pipeline hat die Daten abgewiesen, keine Lieferung |
| 3 | der Versuch ist gescheitert, bevor die Daten beurteilt waren |
| 4 | nach der Wartezeit noch in Verarbeitung; der Versuch läuft weiter, die Status-URL steht in der Ausgabe |
