using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DateAddedToDateAired;

/// <summary>Observes normal library updates after metadata import; it never writes media or NFO files.</summary>
public sealed class ReleaseDateUpdateService : IHostedService, IDisposable
{
    private const int MaxPendingItems = 256;
    private readonly ILibraryManager _libraryManager;
    private readonly ReleaseDateProcessor _processor;
    private readonly ILogger<ReleaseDateUpdateService> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _pendingItems = new();
    private readonly SemaphoreSlim _processingSlots = new(4, 4);
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _stopping;
    public ReleaseDateUpdateService(ILibraryManager libraryManager, ReleaseDateProcessor processor, ILogger<ReleaseDateUpdateService> logger) => (_libraryManager, _processor, _logger) = (libraryManager, processor, logger);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleLock)
        {
            _stopping = new CancellationTokenSource();
            _libraryManager.ItemUpdated += OnItemUpdated;
        }
        _logger.LogInformation("Date Added To Date Aired automatic processing started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? stopping;
        Task[] pending;
        lock (_lifecycleLock)
        {
            _libraryManager.ItemUpdated -= OnItemUpdated;
            stopping = _stopping;
            _stopping = null;
            pending = _pendingItems.Values.ToArray();
        }

        if (stopping is not null)
        {
            await stopping.CancelAsync().ConfigureAwait(false);
        }

        await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            _libraryManager.ItemUpdated -= OnItemUpdated;
        }
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs args)
    {
        if (!Plugin.Instance.Configuration.ProcessNewItemsAutomatically || !IsEnabledSupportedItem(args.Item))
        {
            return;
        }

        TaskCompletionSource completion;
        CancellationToken cancellationToken;
        lock (_lifecycleLock)
        {
            if (_stopping is not { IsCancellationRequested: false } stopping || _pendingItems.Count >= MaxPendingItems)
            {
                _logger.LogDebug("Skipping automatic Date Added processing for {ItemId}: service is stopping or the bounded queue is full.", args.Item.Id);
                return;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pendingItems.TryAdd(args.Item.Id, completion.Task))
            {
                return;
            }

            cancellationToken = stopping.Token;
        }

        _ = ProcessChangedItemAsync(args, completion, cancellationToken);
    }

    private async Task ProcessChangedItemAsync(ItemChangeEventArgs args, TaskCompletionSource completion, CancellationToken cancellationToken)
    {
        try
        {
            // Our persistence call produces another ItemUpdated event; unchanged DateCreated exits without a write.
            var slotAcquired = false;
            try
            {
                await _processingSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
                slotAcquired = true;
                await _processor.ProcessAsync(args.Item, args.Parent, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (slotAcquired)
                {
                    _processingSlots.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not apply a release/air date to {ItemId} ({Name}).", args.Item.Id, args.Item.Name);
        }
        finally
        {
            _pendingItems.TryRemove(args.Item.Id, out _);
            completion.TrySetResult();
        }
    }

    private static bool IsEnabledSupportedItem(MediaBrowser.Controller.Entities.BaseItem item)
        => item switch
        {
            MediaBrowser.Controller.Entities.Movies.Movie => Plugin.Instance.Configuration.EnableMovies,
            MediaBrowser.Controller.Entities.TV.Episode => Plugin.Instance.Configuration.EnableTvEpisodes,
            _ => false
        };
}
