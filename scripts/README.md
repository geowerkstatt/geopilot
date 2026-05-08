# Plugin Scripts

Scripts for fetching pipeline processor plugins into the geopilot development environment.

Plugins are placed into `src/Geopilot.Api/Plugins/<PluginName>/` which is gitignored.

## Local Copy

Copy a locally built plugin (for development alongside the plugin source):

```powershell
# Generic — any plugin
.\Copy-Plugin.ps1 -SourcePath "C:\Projects\my-plugin\bin\Debug\net10.0" -PluginName "MyPlugin"

# VSA-Checker-Analytics — defaults to sibling directory (../vsa-checker-analytics)
.\Copy-VsaPlugin.ps1                                          # Debug build (default)
.\Copy-VsaPlugin.ps1 -Configuration Release                   # Release build
.\Copy-VsaPlugin.ps1 -RepoPath "D:\repos\vsa-checker-analytics" # Custom checkout location
```

## Download from GitHub Release

Download a plugin from a GitHub release:

```powershell
# Generic — any plugin
.\Download-Plugin.ps1 -ReleaseUrl "https://github.com/owner/repo/releases/" -PluginName "MyPlugin"

# VSA-Checker-Analytics
.\Download-VsaPlugin.ps1           # Latest release
.\Download-VsaPlugin.ps1 -Tag v1.0.1  # Specific version
```

### Authentication

Private repositories require a GitHub token with **Contents: Read** permission:

```powershell
$env:GITHUB_TOKEN = "ghp_..."
.\Download-VsaPlugin.ps1

# Or pass it directly
.\Download-VsaPlugin.ps1 -Token "ghp_..."
```

## Configuration

After fetching a plugin, register it in `appsettings.Development.json`:

```json
"Pipeline": {
  "Plugins": ["Plugins\\VsaCheckerAnalytics\\VSACheckerAnalytics.dll"]
}
```
