# Date Added To Date Aired

**Date Added To Date Aired** is a server-side Jellyfin plugin for Jellyfin Server **12.2**. It changes Jellyfin's internal `DateCreated`/Date Added value so native Latest and Recently Added queries use the media's real date:

- Movies: `DateCreated ← PremiereDate` (release date)
- TV series: `DateCreated ← PremiereDate` (series premiere date)
- TV seasons: `DateCreated ← PremiereDate`, falling back to the earliest dated episode
- TV episodes: `DateCreated ← PremiereDate` (first-air date)

The plugin does not inject or modify Jellyfin Web, and it does not modify NFO files, media files, clients, or Jellyfin's SQLite database directly. Native Recently Added presentation can still vary by Jellyfin client; the plugin changes the server-side value used for its ordering.

## Compatibility and safety

- Built against Jellyfin 12.2 public packages (`net10.0`). This personal-server build no longer supports Jellyfin 10.11.x.
- Only `Movie`, `Series`, `Season`, and `Episode` items are processed. Music, photos, collections, and other types are untouched.
- An item with no `PremiereDate` is skipped. Future dates are skipped by default and can be allowed in configuration.
- Updates use Jellyfin's `ILibraryManager.UpdateItemAsync` API. The scheduled task is cancellable; automatic processing is bounded and deduplicated per item.
- Changes are persistent Jellyfin metadata. Disabling or uninstalling the plugin stops future processing but does **not** restore original Date Added values. Back up Jellyfin before processing an existing library.

## Configuration

Open **Dashboard → Plugins → Date Added To Date Aired**:

- **Enable Movies** — process movie release dates.
- **Enable TV Series, Seasons & Episodes** — process TV premiere and air dates.
- **Process new items automatically after metadata updates** — process imports and metadata refreshes.
- **Leave future release/air dates unchanged** — enabled by default.

Settings are stored in Jellyfin's plugin configuration (`Jellyfin.Plugin.DateAddedToDateAired.xml`). Saving a disabled option genuinely disables its corresponding automatic processing; the existing-library task also respects Movie/TV and future-date settings. The page includes **Run update now** for a one-click task run.

For existing libraries run **Dashboard → Scheduled Tasks → Apply Release/Air Dates to Date Added**, or use **Run update now**. Review the task summary in Jellyfin's server log and the daily `DateAddedToDateAired_yyyyMMdd.log` plugin activity log.

## Installation

### Plugin repository (after the first release is published)

1. Open **Dashboard → Plugins → Repositories → Add Repository**.
2. Enter the repository manifest URL: `https://raw.githubusercontent.com/xEkulx/Jellyfin.Plugin.DateAddedToDateAired/main/manifest.json`.
3. Open the Plugin Catalog, install **Date Added To Date Aired**, then restart Jellyfin.

The manifest intentionally has no release entry until a GitHub Release exists. A release URL and checksum must never be guessed.

### Manual installation — Windows and Linux

1. Download the versioned ZIP from the GitHub Releases page and verify its SHA-256 file.
2. Stop Jellyfin.
3. Extract the ZIP into a new directory beneath Jellyfin's data-directory `plugins` folder, for example:

   - Windows: `%ProgramData%\Jellyfin\Server\plugins\DateAddedToDateAired_2.0.1`
   - Linux package/Docker: `/var/lib/jellyfin/plugins/DateAddedToDateAired_2.0.1` or the container's mounted Jellyfin data directory.

4. Confirm the directory contains `Jellyfin.Plugin.DateAddedToDateAired.dll` directly (no extra nested ZIP directory), then start Jellyfin.

Upgrades use a new versioned plugin folder. Remove the prior version only after a successful restart and validation.

## Build and package

Requires the .NET 9 SDK:

```powershell
dotnet test .\Jellyfin.Plugin.DateAddedToDateAired.sln
dotnet publish .\Jellyfin.Plugin.DateAddedToDateAired\Jellyfin.Plugin.DateAddedToDateAired.csproj -c Release
.\scripts\package-release.ps1 -Version 2.0.1
```

The packaging script emits a DLL-only ZIP, SHA-256 checksum, and release notes in `artifacts\release`.

## Release process

Tag `v2.0.1` after review. The GitHub Actions workflow tests, builds, packages, checks the ZIP contents, creates the GitHub Release, then updates `manifest.json` with the release's real download URL, MD5 checksum required by Jellyfin manifests, and timestamp. GitHub Pages is optional; GitHub's raw HTTPS manifest URL above is sufficient.

## Attribution and license

This repository is a modified derivative of [verybadsoldier/Jellyfin.Plugin.DateAddedAdvanced](https://github.com/verybadsoldier/Jellyfin.Plugin.DateAddedAdvanced), originally developed by **verybadsoldier**. The local Git history and upstream releases identify that author. This fork substantially changes its purpose and implementation; it is not the upstream DateAddedAdvanced plugin.

The project remains licensed under the [GNU GPL v3.0](LICENSE). Keep the license and this attribution with redistributed modified versions.
