using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Manually applies release and air dates to the existing library.</summary>
public sealed class ApplyReleaseAirDatesTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ReleaseDateProcessor _processor;
    private readonly ILogger<ApplyReleaseAirDatesTask> _logger;
    public ApplyReleaseAirDatesTask(ILibraryManager libraryManager, ReleaseDateProcessor processor, ILogger<ApplyReleaseAirDatesTask> logger) => (_libraryManager, _processor, _logger) = (libraryManager, processor, logger);

    public string Name => "Apply Release/Air Dates to Date Added";
    public string Key => "DateAddedToDateAired.ApplyReleaseAirDates";
    public string Description => "Sets Movie and TV Series, Season, and Episode Date Added from imported release or air dates.";
    public string Category => "Library";
    public bool IsHidden => false;
    public bool IsEnabled => true;
    public bool IsLogged => true;
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var movies = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Movie], Recursive = true });
        var series = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Series], Recursive = true });
        var seasons = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Season], Recursive = true });
        var episodes = _libraryManager.GetItemList(new InternalItemsQuery { IncludeItemTypes = [BaseItemKind.Episode], Recursive = true });
        var counts = new Counts();
        var total = movies.Count + series.Count + seasons.Count + episodes.Count;
        var complete = 0;
        foreach (var item in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(item, counts, cancellationToken).ConfigureAwait(false);
            progress.Report(++complete * 100d / Math.Max(1, total));
        }

        foreach (var item in series)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(item, counts, cancellationToken).ConfigureAwait(false);
            progress.Report(++complete * 100d / Math.Max(1, total));
        }

        foreach (var item in seasons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(item, counts, cancellationToken).ConfigureAwait(false);
            progress.Report(++complete * 100d / Math.Max(1, total));
        }

        foreach (var item in episodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(item, counts, cancellationToken).ConfigureAwait(false);
            progress.Report(++complete * 100d / Math.Max(1, total));
        }

        _logger.LogInformation("Apply Release/Air Dates complete. Movies scanned/updated: {MoviesScanned}/{MoviesUpdated}; Series scanned/updated: {SeriesScanned}/{SeriesUpdated}; Seasons scanned/updated: {SeasonsScanned}/{SeasonsUpdated}; Episodes scanned/updated: {EpisodesScanned}/{EpisodesUpdated}; Missing release/air date: {Missing}; Future dates skipped: {Future}; Already correct: {Correct}; Errors: {Errors}.", counts.MoviesScanned, counts.MoviesUpdated, counts.SeriesScanned, counts.SeriesUpdated, counts.SeasonsScanned, counts.SeasonsUpdated, counts.EpisodesScanned, counts.EpisodesUpdated, counts.Missing, counts.Future, counts.Correct, counts.Errors);
        Plugin.Instance.ActivityLog.Information($"Scheduled update complete. Movies {counts.MoviesScanned}/{counts.MoviesUpdated}; Series {counts.SeriesScanned}/{counts.SeriesUpdated}; Seasons {counts.SeasonsScanned}/{counts.SeasonsUpdated}; Episodes {counts.EpisodesScanned}/{counts.EpisodesUpdated}; Missing {counts.Missing}; Future {counts.Future}; Correct {counts.Correct}; Errors {counts.Errors}.");
    }

    private async Task ProcessOneAsync(BaseItem item, Counts counts, CancellationToken cancellationToken)
    {
        switch (item)
        {
            case Movie: counts.MoviesScanned++; break;
            case Series: counts.SeriesScanned++; break;
            case Season: counts.SeasonsScanned++; break;
            case Episode: counts.EpisodesScanned++; break;
        }
        try
        {
            switch (await _processor.ProcessAsync(item, null, cancellationToken).ConfigureAwait(false))
            {
                case ProcessResult.Updated:
                    switch (item)
                    {
                        case Movie: counts.MoviesUpdated++; break;
                        case Series: counts.SeriesUpdated++; break;
                        case Season: counts.SeasonsUpdated++; break;
                        case Episode: counts.EpisodesUpdated++; break;
                    }
                    break;
                case ProcessResult.MissingPremiereDate: counts.Missing++; break;
                case ProcessResult.FutureDateSkipped: counts.Future++; break;
                case ProcessResult.AlreadyCorrect: counts.Correct++; break;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            counts.Errors++;
            _logger.LogWarning(ex, "Failed to process {ItemId} ({Name}); continuing.", item.Id, item.Name);
            Plugin.Instance.ActivityLog.Warning($"Scheduled update failed for '{item.Name}' ({item.Id}): {ex.Message}");
        }
    }

    private sealed class Counts
    {
        public int MoviesScanned { get; set; }
        public int MoviesUpdated { get; set; }
        public int SeriesScanned { get; set; }
        public int SeriesUpdated { get; set; }
        public int SeasonsScanned { get; set; }
        public int SeasonsUpdated { get; set; }
        public int EpisodesScanned { get; set; }
        public int EpisodesUpdated { get; set; }
        public int Missing { get; set; }
        public int Future { get; set; }
        public int Correct { get; set; }
        public int Errors { get; set; }
    }
}
