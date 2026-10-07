# Date Added To Date Aired

Server-side Jellyfin plugin for **Jellyfin Server 10.11.8**. It makes native Latest/Recently Added use real release or air dates by changing only Jellyfin's persisted `DateCreated` value:

- `Movie.DateCreated` is set to `Movie.PremiereDate`.
- `Episode.DateCreated` is set to `Episode.PremiereDate` (Jellyfin's first-air-date field).

It deliberately does not modify NFO files, media files, clients, Series, Seasons, music, photos, or other item types. `dateadded` in Sonarr/Radarr NFO files remains untouched.

## Safety

Missing premiere/air dates leave `DateCreated` untouched. Future dates are also left untouched by default (configurable). Changes use Jellyfin's normal `ILibraryManager.UpdateItemAsync` persistence API. The update listener writes only when the stored value differs, so its own `ItemUpdated` notification terminates without another write.

Changes are persistent Jellyfin metadata. Disabling or removing the plugin stops future processing but does not restore previous values. A normal metadata refresh can change values depending on its metadata providers; run the task again if needed. This plugin intentionally has no speculative restore mechanism.

## Existing libraries and new imports

Run **Dashboard → Scheduled Tasks → Apply Release/Air Dates to Date Added** to process an existing library. It is cancellable, skips already-correct items, continues after individual failures, and logs counts.

For new imports and metadata refreshes, the server-side `ItemUpdated` listener applies the mapping after Jellyfin has imported metadata. Enable or disable Movies, TV Episodes, automatic processing, and the future-date policy in the plugin configuration.

## Build and install

Build with the .NET 9 SDK:

```powershell
dotnet publish .\Jellyfin.Plugin.DateAddedToDateAired\Jellyfin.Plugin.DateAddedToDateAired.csproj -c Release -o .\bin
```

Copy `Jellyfin.Plugin.DateAddedToDateAired.dll` to a dedicated plugin directory such as `plugins/Jellyfin.Plugin.DateAddedToDateAired`, then restart Jellyfin. Do not install it into the old DateAddedAdvanced directory.

## Test procedure

1. Back up the Jellyfin data directory.
2. Install the DLL and restart Jellyfin 10.11.8.
3. Confirm the configuration defaults, then run the scheduled task.
4. Inspect a movie/episode through the API or database-backed metadata view: its Date Added must match the imported `PremiereDate` date.
5. Add an old item and a yesterday-dated item, then use each native client’s normal Latest/Recently Added view. The old item should not rank as newly added; the yesterday item should.

Native clients were not tested by this repository; this plugin changes the server-side field that Jellyfin 10.11.8 orders and groups for Latest.
