using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DateAddedToDateAired.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.DateAddedToDateAired.Tests;

[CollectionDefinition("PluginTests", DisableParallelization = true)]
public sealed class PluginTestCollection;

[Collection("PluginTests")]
public sealed class ReleaseDateProcessorTests
{
    [Fact]
    public async Task Movie_UsesCalendarPremiereDateAtUtcMidnight()
    {
        using var helper = new PluginTestHelper();
        var movie = new Movie { PremiereDate = new DateTime(1999, 3, 31, 18, 0, 0, DateTimeKind.Local), DateCreated = DateTime.UtcNow };
        var (processor, library) = CreateProcessor();

        var result = await processor.ProcessAsync(movie, new Folder(), CancellationToken.None);

        Assert.Equal(ProcessResult.Updated, result);
        Assert.Equal(new DateTime(1999, 3, 31, 0, 0, 0, DateTimeKind.Utc), movie.DateCreated);
        library.Verify(x => x.UpdateItemAsync(movie, It.IsAny<BaseItem>(), ItemUpdateType.MetadataEdit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Episode_UsesCalendarPremiereDateAtUtcMidnight()
    {
        using var helper = new PluginTestHelper();
        var episode = new Episode { PremiereDate = new DateTime(2015, 6, 12), DateCreated = DateTime.UtcNow };
        var (processor, _) = CreateProcessor();

        await processor.ProcessAsync(episode, new Folder(), CancellationToken.None);

        Assert.Equal(new DateTime(2015, 6, 12, 0, 0, 0, DateTimeKind.Utc), episode.DateCreated);
    }

    [Fact]
    public async Task MissingFutureAlreadyCorrectAndUnsupportedItemsAreNotPersisted()
    {
        using var helper = new PluginTestHelper();
        var (processor, library) = CreateProcessor();
        var parent = new Folder();
        var missing = new Movie { DateCreated = DateTime.UtcNow };
        var future = new Episode { PremiereDate = DateTime.UtcNow.Date.AddDays(1), DateCreated = DateTime.UtcNow };
        var correct = new Movie { PremiereDate = new DateTime(1999, 3, 31), DateCreated = new DateTime(1999, 3, 31, 0, 0, 0, DateTimeKind.Utc) };
        var unsupported = new Folder { DateCreated = DateTime.UtcNow };

        Assert.Equal(ProcessResult.MissingPremiereDate, await processor.ProcessAsync(missing, parent, CancellationToken.None));
        Assert.Equal(ProcessResult.FutureDateSkipped, await processor.ProcessAsync(future, parent, CancellationToken.None));
        Assert.Equal(ProcessResult.AlreadyCorrect, await processor.ProcessAsync(correct, parent, CancellationToken.None));
        Assert.Equal(ProcessResult.Unsupported, await processor.ProcessAsync(unsupported, parent, CancellationToken.None));
        library.Verify(x => x.UpdateItemAsync(It.IsAny<BaseItem>(), It.IsAny<BaseItem>(), It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DisabledMovieProcessingDoesNotPersist()
    {
        using var helper = new PluginTestHelper(new PluginConfiguration { EnableMovies = false });
        var (processor, library) = CreateProcessor();
        var movie = new Movie { PremiereDate = new DateTime(1999, 3, 31), DateCreated = DateTime.UtcNow };

        var result = await processor.ProcessAsync(movie, new Folder(), CancellationToken.None);

        Assert.Equal(ProcessResult.Disabled, result);
        library.Verify(x => x.UpdateItemAsync(It.IsAny<BaseItem>(), It.IsAny<BaseItem>(), It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ReleaseDateProcessor Processor, Mock<ILibraryManager> Library) CreateProcessor()
    {
        var library = new Mock<ILibraryManager>();
        library.Setup(x => x.UpdateItemAsync(It.IsAny<BaseItem>(), It.IsAny<BaseItem>(), It.IsAny<ItemUpdateType>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return (new ReleaseDateProcessor(library.Object, Mock.Of<ILogger<ReleaseDateProcessor>>()), library);
    }
}

internal sealed class PluginTestHelper : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "DateAddedToDateAiredTests_" + Guid.NewGuid().ToString("N"));

    public PluginTestHelper(PluginConfiguration? configuration = null)
    {
        Directory.CreateDirectory(_path);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(x => x.PluginConfigurationsPath).Returns(_path);
        paths.SetupGet(x => x.ConfigurationDirectoryPath).Returns(_path);
        paths.SetupGet(x => x.PluginsPath).Returns(_path);
        paths.SetupGet(x => x.DataPath).Returns(_path);
        paths.SetupGet(x => x.ProgramDataPath).Returns(_path);
        var plugin = new Plugin(paths.Object, Mock.Of<IXmlSerializer>());
        plugin.UpdateConfiguration(configuration ?? new PluginConfiguration());
    }

    public void Dispose()
    {
        if (Directory.Exists(_path)) Directory.Delete(_path, true);
    }
}
