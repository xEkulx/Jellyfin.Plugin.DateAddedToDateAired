using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Observes normal library updates after metadata import; it never writes media or NFO files.</summary>
public sealed class ReleaseDateUpdateService : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ReleaseDateProcessor _processor;
    private readonly ILogger<ReleaseDateUpdateService> _logger;
    public ReleaseDateUpdateService(ILibraryManager libraryManager, ReleaseDateProcessor processor, ILogger<ReleaseDateUpdateService> logger) => (_libraryManager, _processor, _logger) = (libraryManager, processor, logger);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnItemUpdated;
        _logger.LogInformation("Date Added To Date Aired automatic processing started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        return Task.CompletedTask;
    }

    public void Dispose() => _libraryManager.ItemUpdated -= OnItemUpdated;

    private void OnItemUpdated(object? sender, ItemChangeEventArgs args)
    {
        if (Plugin.Instance.Configuration.ProcessNewItemsAutomatically) _ = ProcessChangedItemAsync(args);
    }

    private async Task ProcessChangedItemAsync(ItemChangeEventArgs args)
    {
        try
        {
            // Our persistence call produces another ItemUpdated event; unchanged DateCreated exits without a write.
            await _processor.ProcessAsync(args.Item, args.Parent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not apply a release/air date to {ItemId} ({Name}).", args.Item.Id, args.Item.Name);
        }
    }
}
