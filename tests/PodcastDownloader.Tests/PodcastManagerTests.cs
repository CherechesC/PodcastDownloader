using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;
using PodcastDownloader.Core.Services;
using PodcastDownloader.Core.Storage;

namespace PodcastDownloader.Tests;

public class PodcastManagerTests
{
    [Fact]
    public async Task SubscribeAsync_AddsPodcastToRepository()
    {
        // Arrange
        var samplePodcast = CreateSamplePodcast();
        var feedService = new StubPodcastFeedService(samplePodcast);
        var repository = new InMemoryPodcastRepository();
        var downloadService = new StubDownloadService();
        var sut = new PodcastManager(feedService, repository, downloadService, NullLogger<PodcastManager>.Instance);

        // Act
        var result = await sut.SubscribeAsync(samplePodcast.FeedUri);
        var stored = await repository.GetAsync(samplePodcast.Id);

        // Assert
        result.Title.Should().Be(samplePodcast.Title);
        stored.Should().NotBeNull();
        stored!.Episodes.Should().HaveCount(samplePodcast.Episodes.Count);
    }

    [Fact]
    public async Task DownloadEpisodeAsync_MarksEpisodeAsCompleted()
    {
        // Arrange
        var samplePodcast = CreateSamplePodcast();
        var feedService = new StubPodcastFeedService(samplePodcast);
        var repository = new InMemoryPodcastRepository();
        var downloadService = new StubDownloadService();
        var sut = new PodcastManager(feedService, repository, downloadService, NullLogger<PodcastManager>.Instance);

        await sut.SubscribeAsync(samplePodcast.FeedUri);
        var stored = (await repository.GetAsync(samplePodcast.Id))!;
        var episode = stored.Episodes.First();

        // Act
        await sut.DownloadEpisodeAsync(stored.Id, episode.Id);
        var refreshed = await repository.GetAsync(stored.Id);

        // Assert
        refreshed!.Episodes.First(e => e.Id == episode.Id).DownloadStatus.Should().Be(DownloadStatus.Completed);
        refreshed!.Episodes.First(e => e.Id == episode.Id).LocalFilePath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SubscribeAsync_DownloadsEpisodeArtwork()
    {
        // Arrange
        var samplePodcast = CreateSamplePodcast(3);
        var feedService = new StubPodcastFeedService(samplePodcast);
        var repository = new InMemoryPodcastRepository();
        var downloadService = new StubDownloadService();
        var sut = new PodcastManager(feedService, repository, downloadService, NullLogger<PodcastManager>.Instance);

        // Act
        await sut.SubscribeAsync(samplePodcast.FeedUri);

        // Assert
        downloadService.ArtworkDownloadEpisodeIds.Should().HaveCount(samplePodcast.Episodes.Count);
        var stored = await repository.GetAsync(samplePodcast.Id);
        stored.Should().NotBeNull();
        stored!.Episodes.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.ArtworkFilePath));
    }

    [Fact]
    public async Task DownloadAllEpisodesAsync_DownloadsEveryEpisode()
    {
        // Arrange
        var samplePodcast = CreateSamplePodcast(25);
        var feedService = new StubPodcastFeedService(samplePodcast);
        var repository = new InMemoryPodcastRepository();
        var downloadService = new StubDownloadService();
        var sut = new PodcastManager(feedService, repository, downloadService, NullLogger<PodcastManager>.Instance);

        await sut.SubscribeAsync(samplePodcast.FeedUri);

        // Act
        await sut.DownloadAllEpisodesAsync(samplePodcast.Id, chunkSize: 10);

        // Assert
        var stored = await repository.GetAsync(samplePodcast.Id);
        stored.Should().NotBeNull();
        stored!.Episodes.Should().OnlyContain(e => e.DownloadStatus == DownloadStatus.Completed);
        downloadService.DownloadedEpisodeIds.Should().HaveCount(samplePodcast.Episodes.Count);
    }

    private static Podcast CreateSamplePodcast() => CreateSamplePodcast(1);

    private static Podcast CreateSamplePodcast(int episodeCount)
    {
        var feedUri = new Uri("https://example.com/feed.xml");
        var podcast = new Podcast(Podcast.CreateId(feedUri), feedUri, "Sample Podcast");
        podcast.UpdateMetadata(podcast.Title, "A test podcast", null, DateTimeOffset.UtcNow);

        var episodes = Enumerable.Range(1, episodeCount)
            .Select(index =>
            {
                var episode = new Episode(Guid.NewGuid().ToString("N"), $"Episode {index}", new Uri($"https://example.com/ep{index}.mp3"));
                episode.UpdateMetadata(
                    $"Summary {index}",
                    TimeSpan.FromMinutes(20 + index),
                    DateTimeOffset.UtcNow.AddDays(-index),
                    new Uri($"https://example.com/ep{index}.jpg"),
                    index);
                return episode;
            })
            .ToList();

        podcast.MergeEpisodes(episodes);
        return podcast;
    }

    private sealed class StubPodcastFeedService : IPodcastFeedService
    {
        private readonly Podcast _podcast;

        public StubPodcastFeedService(Podcast podcast)
        {
            _podcast = podcast;
        }

        public Task<Podcast> GetPodcastAsync(Uri feedUri, CancellationToken cancellationToken = default)
        {
            // Return a fresh copy each time to mimic feed retrieval.
            var copy = new Podcast(_podcast.Id, _podcast.FeedUri, _podcast.Title);
            copy.UpdateMetadata(_podcast.Title, _podcast.Description, _podcast.ArtworkUri, _podcast.LastUpdated);
            copy.MergeEpisodes(_podcast.Episodes.Select(CloneEpisode));
            return Task.FromResult(copy);
        }

        private static Episode CloneEpisode(Episode source)
        {
            var clone = new Episode(source.Id, source.Title, source.MediaUri);
            clone.UpdateMetadata(source.Summary, source.Duration, source.PublishedAt, source.ArtworkUri, source.EpisodeNumber);
            if (!string.IsNullOrWhiteSpace(source.LocalFilePath))
            {
                clone.MarkCompleted(source.LocalFilePath!, source.ArtworkFilePath);
            }

            return clone;
        }
    }

    private sealed class StubDownloadService : IPodcastDownloadService
    {
        public List<string> DownloadedEpisodeIds { get; } = new();

        public List<string> ArtworkDownloadEpisodeIds { get; } = new();

        public Task DownloadEpisodeAsync(Podcast podcast, Episode episode, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            DownloadedEpisodeIds.Add(episode.Id);
            progress?.Report(1);

            var artworkPath = Path.Combine(Path.GetTempPath(), episode.Id + "-art.jpg");
            episode.SetArtworkFilePath(artworkPath);

            var tempPath = Path.Combine(Path.GetTempPath(), episode.Id + ".mp3");
            episode.MarkCompleted(tempPath, artworkPath);
            return Task.CompletedTask;
        }

        public Task<string?> DownloadEpisodeArtworkAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
        {
            ArtworkDownloadEpisodeIds.Add(episode.Id);
            var artworkPath = Path.Combine(Path.GetTempPath(), episode.Id + "-art.jpg");
            episode.SetArtworkFilePath(artworkPath);
            return Task.FromResult<string?>(artworkPath);
        }
    }
}
