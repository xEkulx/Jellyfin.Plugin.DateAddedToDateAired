using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateAddedToDateAired;

public enum ProcessResult { Updated, AlreadyCorrect, MissingPremiereDate, FutureDateSkipped, Disabled, Unsupported, MissingParent }

/// <summary>Applies only the Movie/Episode rule through Jellyfin's normal persistence API.</summary>
public sealed class ReleaseDateProcessor
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ReleaseDateProcessor> _logger;
    public ReleaseDateProcessor(ILibraryManager libraryManager, ILogger<ReleaseDateProcessor> logger) => (_libraryManager, _logger) = (libraryManager, logger);

    public async Task<ProcessResult> ProcessAsync(BaseItem item, BaseItem? parent, CancellationToken cancellationToken)
    {
        if (!IsEnabled(item)) return item is Movie or Episode ? ProcessResult.Disabled : ProcessResult.Unsupported;
        if (!item.PremiereDate.HasValue) return ProcessResult.MissingPremiereDate;

        // BaseItem.PremiereDate is Jellyfin's debut/first-air field. DateCreated is stored as UTC.
        var target = DateTime.SpecifyKind(item.PremiereDate.Value.Date, DateTimeKind.Utc);
        if (Plugin.Instance.Configuration.SkipFuturePremiereDates && target.Date > DateTime.UtcNow.Date) return ProcessResult.FutureDateSkipped;
        if (item.DateCreated == target) return ProcessResult.AlreadyCorrect;

        parent ??= item.GetParent();
        if (parent is null)
        {
            _logger.LogWarning("Cannot update DateCreated for {ItemId} ({Name}) because it has no parent.", item.Id, item.Name);
            return ProcessResult.MissingParent;
        }

        item.DateCreated = target;
        await _libraryManager.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Set DateCreated for {ItemType} {ItemId} ({Name}) to {DateCreated}.", item.GetType().Name, item.Id, item.Name, target);
        return ProcessResult.Updated;
    }

    private static bool IsEnabled(BaseItem item) => item switch
    {
        Movie => Plugin.Instance.Configuration.EnableMovies,
        Episode => Plugin.Instance.Configuration.EnableTvEpisodes,
        _ => false
    };
}
