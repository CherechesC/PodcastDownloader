using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;
using PodcastDownloader.Core.Storage;
using Xunit;

namespace PodcastDownloader.Tests;

public class JsonFilePodcastRepositoryTests : IAsyncLifetime
{
    private readonly string _rootPath;
    private readonly StubStorageRootProvider _rootProvider;

    public JsonFilePodcastRepositoryTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "PodcastDownloader.Tests", Guid.NewGuid().ToString("N"));
        _rootProvider = new StubStorageRootProvider(_rootPath);
    }

    [Fact]
    public async Task UpsertAsync_PersistsPodcastToDisk()
    {
    var repository = CreateRepository();
    var podcast = CreateSamplePodcast();

        await repository.UpsertAsync(podcast);

        var metadataPath = Path.Combine(_rootPath, "podcasts.json");
        File.Exists(metadataPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(metadataPath);
        json.Should().NotContain(_rootPath, "file paths should be stored relative to the storage root");
        json.Should().Contain("media/");

        var reloadedRepository = CreateRepository();
        var stored = await reloadedRepository.GetAsync(podcast.Id);

    stored.Should().NotBeNull();
    stored!.Episodes.Should().HaveCount(1);

    var originalEpisode = podcast.Episodes.First();
    var storedEpisode = stored.Episodes.First();
    storedEpisode.DownloadStatus.Should().Be(originalEpisode.DownloadStatus);
    storedEpisode.EpisodeNumber.Should().Be(originalEpisode.EpisodeNumber);
    storedEpisode.ArtworkFilePath.Should().Be(originalEpisode.ArtworkFilePath);
    }

    [Fact]
    public async Task RemoveAsync_RemovesPodcastFromDisk()
    {
    var repository = CreateRepository();
    var podcast = CreateSamplePodcast();

        await repository.UpsertAsync(podcast);
        await repository.RemoveAsync(podcast.Id);

        var reloadedRepository = CreateRepository();
        var stored = await reloadedRepository.GetAsync(podcast.Id);
        stored.Should().BeNull();
    }

    private JsonFilePodcastRepository CreateRepository()
    {
        return new JsonFilePodcastRepository(_rootProvider, NullLogger<JsonFilePodcastRepository>.Instance);
    }

    private Podcast CreateSamplePodcast()
    {
        var feedUri = new Uri("https://example.com/feed.xml");
        var podcast = new Podcast(Podcast.CreateId(feedUri), feedUri, "Sample Podcast");
        podcast.UpdateMetadata(podcast.Title, "Description", null, DateTimeOffset.UtcNow);

        var episode = new Episode(Guid.NewGuid().ToString("N"), "Episode 1", new Uri("https://example.com/episodes/1.mp3"));
        episode.UpdateMetadata("Summary", TimeSpan.FromMinutes(30), DateTimeOffset.UtcNow.AddDays(-1), null, 5);
        var mediaDirectory = Path.Combine(_rootPath, "media");
        Directory.CreateDirectory(mediaDirectory);
        var audioPath = Path.Combine(mediaDirectory, episode.Id + ".mp3");
        var artworkPath = Path.Combine(mediaDirectory, episode.Id + "-art.jpg");
        episode.MarkCompleted(audioPath, artworkPath);
        podcast.MergeEpisodes(new[] { episode });

        return podcast;
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_rootPath);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, true);
        }

        return Task.CompletedTask;
    }

    private sealed class StubStorageRootProvider : IStorageRootProvider
    {
        private string _rootPath;

        public StubStorageRootProvider(string rootPath)
        {
            _rootPath = rootPath;
        }

        public string GetRootPath() => _rootPath;

        public void SetRootPath(string rootPath)
        {
            _rootPath = rootPath;
        }
    }
}
