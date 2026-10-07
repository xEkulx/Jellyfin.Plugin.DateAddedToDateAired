using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.DateAddedToDateAired.Tests;

[Collection("PluginTests")]
public sealed class ReleaseDateUpdateServiceTests
{
    [Fact]
    public async Task DuplicateAndPluginTriggeredEventsForOneItemPersistOnlyOnce()
    {
        using var helper = new PluginTestHelper();
        var library = new Mock<ILibraryManager>();
        var updateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var movie = new Movie { Id = Guid.NewGuid(), PremiereDate = new DateTime(1999, 3, 31), DateCreated = DateTime.UtcNow };
        var parent = new Folder();
        library.Setup(x => x.UpdateItemAsync(movie, parent, ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref calls);
                updateStarted.TrySetResult();
                library.Raise(x => x.ItemUpdated += null, library.Object, new ItemChangeEventArgs { Item = movie, Parent = parent, UpdateReason = ItemUpdateType.MetadataEdit });
                await releaseUpdate.Task;
            });

        var processor = new ReleaseDateProcessor(library.Object, Mock.Of<ILogger<ReleaseDateProcessor>>());
        using var service = new ReleaseDateUpdateService(library.Object, processor, Mock.Of<ILogger<ReleaseDateUpdateService>>());
        await service.StartAsync(CancellationToken.None);

        library.Raise(x => x.ItemUpdated += null, library.Object, new ItemChangeEventArgs { Item = movie, Parent = parent, UpdateReason = ItemUpdateType.MetadataImport });
        library.Raise(x => x.ItemUpdated += null, library.Object, new ItemChangeEventArgs { Item = movie, Parent = parent, UpdateReason = ItemUpdateType.MetadataImport });
        await updateStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        releaseUpdate.TrySetResult();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, calls);
    }
}
