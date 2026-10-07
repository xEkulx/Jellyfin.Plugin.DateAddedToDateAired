using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DateAddedToDateAired.Configuration;

/// <summary>Configuration for the server-side date mapping.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool EnableMovies { get; set; } = true;
    public bool EnableTvEpisodes { get; set; } = true;
    public bool ProcessNewItemsAutomatically { get; set; } = true;
    public bool SkipFuturePremiereDates { get; set; } = true;
}
