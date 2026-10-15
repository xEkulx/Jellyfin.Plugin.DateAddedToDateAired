using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateAddedToDateAired;

public enum ProcessResult { Updated, AlreadyCorrect, MissingPremiereDate, FutureDateSkipped, Disabled, Unsupported, MissingParent }

/// <summary>Applies the Movie and TV hierarchy rules through Jellyfin's normal persistence API.</summary>
public sealed class ReleaseDateProcessor
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ReleaseDateProcessor> _logger;
    public ReleaseDateProcessor(ILibraryManager libraryManager, ILogger<ReleaseDateProcessor> logger) => (_libraryManager, _logger) = (libraryManager, logger);

    public async Task<ProcessResult> ProcessAsync(BaseItem item, BaseItem? parent, CancellationToken cancellationToken)
    {
        if (!IsSupported(item)) return ProcessResult.Unsupported;
        if (!IsEnabled(item)) return ProcessResult.Disabled;

        var sourceDate = GetSourceDate(item);
        if (!sourceDate.HasValue) return ProcessResult.MissingPremiereDate;

        // BaseItem.PremiereDate is Jellyfin's debut/first-air field. DateCreated is stored as UTC.
        var target = DateTime.SpecifyKind(sourceDate.Value.Date, DateTimeKind.Utc);
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
        _logger.LogInformation(
            "Updated Jellyfin database Date Added for {ItemType} {ItemId} ({Name}) to {DateAddedUtc:O} from release/air date {PremiereDate:yyyy-MM-dd}.",
            item.GetType().Name,
            item.Id,
            item.Name,
            target,
            sourceDate.Value);
        Plugin.Instance.ActivityLog.Information($"Updated {item.GetType().Name} '{item.Name}' ({item.Id}) to {target:O} from release/air date {sourceDate.Value:yyyy-MM-dd}.");
        return ProcessResult.Updated;
    }

    private DateTime? GetSourceDate(BaseItem item)
    {
        if (item.PremiereDate.HasValue)
        {
            return item.PremiereDate.Value;
        }

        if (item is not Season season)
        {
            return null;
        }

        // Some metadata providers do not populate a season PremiereDate. In that case,
        // use its earliest dated episode so the season groups with its first episode.
        return _libraryManager
            .GetItemList(new InternalItemsQuery
            {
                AncestorIds = [season.Id],
                IncludeItemTypes = [BaseItemKind.Episode],
                Recursive = true
            })
            .OfType<Episode>()
            .Where(episode => episode.PremiereDate.HasValue)
            .OrderBy(episode => episode.PremiereDate)
            .Select(episode => episode.PremiereDate)
            .FirstOrDefault();
    }

    private static bool IsSupported(BaseItem item) => item is Movie or Series or Season or Episode;

    private static bool IsEnabled(BaseItem item) => item switch
    {
        Movie => Plugin.Instance.Configuration.EnableMovies,
        Series or Season or Episode => Plugin.Instance.Configuration.EnableTvEpisodes,
        _ => false
    };
}
