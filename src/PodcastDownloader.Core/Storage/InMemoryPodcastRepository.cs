using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Storage;

public class InMemoryPodcastRepository : IPodcastRepository
{
    private readonly Dictionary<string, Podcast> _storage = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<Podcast>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _storage.Values
                .Select(Clone)
                .OrderByDescending(p => p.LastUpdated)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Podcast?> GetAsync(string podcastId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _storage.TryGetValue(podcastId, out var podcast)
                ? Clone(podcast)
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(Podcast podcast, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _storage[podcast.Id] = Clone(podcast);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string podcastId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _storage.Remove(podcastId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Podcast Clone(Podcast source)
    {
        var clone = new Podcast(source.Id, source.FeedUri, source.Title);
        clone.UpdateMetadata(source.Title, source.Description, source.ArtworkUri, source.LastUpdated);
        clone.MergeEpisodes(source.Episodes.Select(Clone));
        return clone;
    }

    private static Episode Clone(Episode source)
    {
        var clone = new Episode(source.Id, source.Title, source.MediaUri);
        clone.UpdateMetadata(source.Summary, source.Duration, source.PublishedAt, source.ArtworkUri, source.EpisodeNumber);
        clone.SetDownloadState(source.DownloadStatus, source.LocalFilePath, source.ArtworkFilePath);
        return clone;
    }
}
