using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DateAddedToDateAired.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Plugin entry point.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ActivityLog = new PluginActivityLog(applicationPaths);
    }

    public static Plugin Instance { get; private set; } = null!;
    public PluginActivityLog ActivityLog { get; }
    public override Guid Id => new("a7fcd5d8-0879-48d1-96f3-b802a9a91128");
    public override string Name => "Date Added To Date Aired";
    public override string Description => "Uses movie release dates and episode air dates for Jellyfin Date Added.";
    public override string ConfigurationFileName => "Jellyfin.Plugin.DateAddedToDateAired.xml";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            DisplayName = Name,
            // Register alongside other dashboard pages so Jellyfin loads the embedded
            // configuration page as an interactive dashboard route.
            EnableInMainMenu = true,
            MenuIcon = "date_range",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
        };

        yield return new PluginPageInfo
        {
            Name = "DateAddedToDateAired.js",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.js"
        };
    }
}
