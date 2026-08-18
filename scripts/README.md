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
