using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Services;

public class PodcastManager
{
    private readonly IPodcastFeedService _feedService;
    private readonly IPodcastRepository _repository;
    private readonly IPodcastDownloadService _downloadService;
    private readonly ILogger<PodcastManager> _logger;

    public PodcastManager(
        IPodcastFeedService feedService,
        IPodcastRepository repository,
        IPodcastDownloadService downloadService,
        ILogger<PodcastManager> logger)
    {
        _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<Podcast>> GetPodcastsAsync(CancellationToken cancellationToken = default)
        => _repository.ListAsync(cancellationToken);

    public async Task<Podcast> SubscribeAsync(Uri feedUri, CancellationToken cancellationToken = default)
    {
        if (feedUri is null)
        {
            throw new ArgumentNullException(nameof(feedUri));
        }

        _logger.LogInformation("Subscribing to podcast feed {FeedUri}", feedUri);

        var podcast = await _feedService.GetPodcastAsync(feedUri, cancellationToken).ConfigureAwait(false);
        await MergeDownloadMetadataAsync(podcast, cancellationToken).ConfigureAwait(false);
        await EnsureEpisodeArtworksAsync(podcast, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertAsync(podcast, cancellationToken).ConfigureAwait(false);

        return podcast;
    }

    public async Task<Podcast> RefreshAsync(string podcastId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetAsync(podcastId, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException($"No podcast found with id '{podcastId}'.");

        _logger.LogInformation("Refreshing podcast {PodcastTitle}", existing.Title);

        var refreshed = await _feedService.GetPodcastAsync(existing.FeedUri, cancellationToken).ConfigureAwait(false);
        await MergeDownloadMetadataAsync(refreshed, cancellationToken, existing).ConfigureAwait(false);
    await EnsureEpisodeArtworksAsync(refreshed, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertAsync(refreshed, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    public Task RemoveAsync(string podcastId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing podcast {PodcastId}", podcastId);
        return _repository.RemoveAsync(podcastId, cancellationToken);
    }

    public async Task DownloadEpisodeAsync(string podcastId, string episodeId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var podcast = await _repository.GetAsync(podcastId, cancellationToken).ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"No podcast found with id '{podcastId}'.");

        var episode = podcast.GetEpisodeById(episodeId)
                      ?? throw new InvalidOperationException($"No episode '{episodeId}' found for podcast '{podcast.Title}'.");

        _logger.LogInformation("Starting download of episode {EpisodeTitle} from podcast {PodcastTitle}", episode.Title, podcast.Title);

        episode.MarkInProgress();
        await _repository.UpsertAsync(podcast, cancellationToken).ConfigureAwait(false);

        try
        {
            await _downloadService.DownloadEpisodeAsync(podcast, episode, progress, cancellationToken).ConfigureAwait(false);
            await _repository.UpsertAsync(podcast, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            episode.MarkFailed();
            await _repository.UpsertAsync(podcast, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Download cancelled for episode {EpisodeTitle}", episode.Title);
            throw;
        }
        catch (Exception ex)
        {
            episode.MarkFailed();
            await _repository.UpsertAsync(podcast, cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Download failed for episode {EpisodeTitle}", episode.Title);
            throw;
        }
    }

    public async Task DownloadAllEpisodesAsync(string podcastId, int chunkSize = 20, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");
        }

        var snapshot = await _repository.GetAsync(podcastId, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidOperationException($"No podcast found with id '{podcastId}'.");

        var episodes = snapshot.Episodes.ToList();
        var total = episodes.Count;
        if (total == 0)
        {
            progress?.Report(1);
            return;
        }

        var completed = 0;

        for (var i = 0; i < episodes.Count; i += chunkSize)
        {
            var chunk = episodes.Skip(i).Take(chunkSize).ToList();
            _logger.LogInformation("Downloading episodes {Start}-{End} of {Total} for podcast {PodcastTitle}", i + 1, Math.Min(i + chunk.Count, total), total, snapshot.Title);

            foreach (var episode in chunk)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!episode.IsDownloaded)
                {
                    await DownloadEpisodeAsync(podcastId, episode.Id, null, cancellationToken).ConfigureAwait(false);
                }

                completed++;
                progress?.Report(Math.Clamp(completed / (double)total, 0d, 1d));
            }
        }
    }

    private async Task MergeDownloadMetadataAsync(Podcast podcast, CancellationToken cancellationToken, Podcast? existing = null)
    {
        Podcast? snapshot = existing;
        if (snapshot is null)
        {
            snapshot = await _repository.GetAsync(podcast.Id, cancellationToken).ConfigureAwait(false);
        }

        if (snapshot is null)
        {
            return;
        }

        var downloadLookup = snapshot.Episodes.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var episode in podcast.Episodes)
        {
            if (downloadLookup.TryGetValue(episode.Id, out var existingEpisode) && existingEpisode.IsDownloaded)
            {
                episode.SetDownloadState(existingEpisode.DownloadStatus, existingEpisode.LocalFilePath, existingEpisode.ArtworkFilePath);
            }
        }
    }

    private async Task EnsureEpisodeArtworksAsync(Podcast podcast, CancellationToken cancellationToken)
    {
        foreach (var episode in podcast.Episodes)
        {
            if (episode.ArtworkUri is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(episode.ArtworkFilePath) && File.Exists(episode.ArtworkFilePath))
            {
                continue;
            }

            try
            {
                await _downloadService.DownloadEpisodeArtworkAsync(podcast, episode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Artwork download cancelled for episode {EpisodeTitle}", episode.Title);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download artwork for episode {EpisodeTitle}", episode.Title);
            }
        }
    }
}
