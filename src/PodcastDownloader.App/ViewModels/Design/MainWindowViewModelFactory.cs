using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using PodcastDownloader.App.Services;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;
using PodcastDownloader.Core.Services;
using PodcastDownloader.Core.Storage;

namespace PodcastDownloader.App.ViewModels.Design;

internal static class MainWindowViewModelFactory
{
    public static MainWindowViewModel CreateDesignInstance()
    {
        var repository = new InMemoryPodcastRepository();
        var feedService = new DesignFeedService();
        var downloadService = new DesignDownloadService();
        var podcastManager = new PodcastManager(feedService, repository, downloadService, NullLogger<PodcastManager>.Instance);
        var imageCache = new DesignImageCache();
        var storageRootProvider = new DesignStorageRootProvider();
        var folderPickerService = new DesignFolderPickerService();
        var podcastDiscoveryService = new DesignPodcastDiscoveryService();

        var viewModel = new MainWindowViewModel(podcastManager, NullLogger<MainWindowViewModel>.Instance, imageCache, storageRootProvider, folderPickerService, podcastDiscoveryService)
        {
            FeedUrl = "https://example.com/feed.xml",
            StatusMessage = "Design-time data",
            DownloadRootPath = storageRootProvider.GetRootPath()
        };

        var samplePodcast = DesignFeedService.CreateSamplePodcast();
        viewModel.Podcasts.Add(new PodcastItemViewModel(samplePodcast, imageCache));
        viewModel.SelectedPodcast = viewModel.Podcasts[0];
        viewModel.SelectedEpisode = viewModel.SelectedPodcast.Episodes[0];

        return viewModel;
    }

    private sealed class DesignFeedService : IPodcastFeedService
    {
        public Task<Podcast> GetPodcastAsync(Uri feedUri, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateSamplePodcast());
        }

        internal static Podcast CreateSamplePodcast()
        {
            var feedUri = new Uri("https://example.com/design-feed");
            var podcast = new Podcast(Podcast.CreateId(feedUri), feedUri, "Design Time Podcast");
            podcast.UpdateMetadata(podcast.Title, "Sample data for the designer.", new Uri("https://picsum.photos/seed/podcast/300/300"), DateTimeOffset.UtcNow);

            var episode1 = new Episode(Guid.NewGuid().ToString("N"), "Introduction", new Uri("https://example.com/episodes/intro.mp3"));
            episode1.UpdateMetadata("Welcome to the sample feed.", TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow.AddDays(-2), new Uri("https://picsum.photos/seed/episode1/200/200"));
            var episode2 = new Episode(Guid.NewGuid().ToString("N"), "Deep Dive", new Uri("https://example.com/episodes/deep-dive.mp3"));
            episode2.UpdateMetadata("Exploring podcasts.", TimeSpan.FromMinutes(42), DateTimeOffset.UtcNow.AddDays(-1), new Uri("https://picsum.photos/seed/episode2/200/200"));
            var tempFile = Path.Combine(Path.GetTempPath(), "design-deep-dive.mp3");
            episode2.MarkCompleted(tempFile);

            podcast.MergeEpisodes(new[] { episode1, episode2 });
            return podcast;
        }
    }

    private sealed class DesignDownloadService : IPodcastDownloadService
    {
        public Task DownloadEpisodeAsync(Podcast podcast, Episode episode, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(1);
            var tempFile = Path.Combine(Path.GetTempPath(), "design-sample.mp3");
            var artworkPath = Path.Combine(Path.GetTempPath(), "design-sample-art.jpg");
            episode.SetArtworkFilePath(artworkPath);
            episode.MarkCompleted(tempFile, artworkPath);
            return Task.CompletedTask;
        }

        public Task<string?> DownloadEpisodeArtworkAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
        {
            var artworkPath = Path.Combine(Path.GetTempPath(), "design-sample-art.jpg");
            episode.SetArtworkFilePath(artworkPath);
            return Task.FromResult<string?>(artworkPath);
        }
    }

    private sealed class DesignImageCache : IImageCache
    {
        public Task<Bitmap?> GetAsync(Uri? uri, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Bitmap?>(null);
        }
    }

    private sealed class DesignStorageRootProvider : IStorageRootProvider
    {
        private string _rootPath = Path.Combine(Path.GetTempPath(), "PodcastDownloader", "DesignData");

        public string GetRootPath()
        {
            return _rootPath;
        }

        public void SetRootPath(string rootPath)
        {
            _rootPath = rootPath;
        }
    }

    private sealed class DesignFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class DesignPodcastDiscoveryService : IPodcastDiscoveryService
    {
        public Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            var results = new List<PodcastSearchResult>
            {
                new PodcastSearchResult("Design Time Podcast",
                    new Uri("https://example.com/design-feed"),
                    "Design Author",
                    "A podcast about design.",
                    new Uri("https://example.com/design-artwork.jpg"),
                    "Design Genre"
                )
            };
            return Task.FromResult<IReadOnlyList<PodcastSearchResult>>(results);
        }
    }
}
