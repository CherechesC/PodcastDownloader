using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Storage;

public class JsonFilePodcastRepository : IPodcastRepository
{
    private readonly IStorageRootProvider _rootProvider;
    private readonly ILogger<JsonFilePodcastRepository> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions;

    private bool _isLoaded;
    private string? _currentRoot;
    private Dictionary<string, Podcast> _podcasts = new(StringComparer.OrdinalIgnoreCase);

    public JsonFilePodcastRepository(IStorageRootProvider rootProvider, ILogger<JsonFilePodcastRepository> logger)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<IReadOnlyList<Podcast>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _podcasts.Values.Select(Clone).OrderByDescending(p => p.LastUpdated).ToList();
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
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _podcasts.TryGetValue(podcastId, out var podcast) ? Clone(podcast) : null;
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
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            _podcasts[podcast.Id] = Clone(podcast);
            await SaveAsync(cancellationToken).ConfigureAwait(false);
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
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            if (_podcasts.Remove(podcastId))
            {
                await SaveAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        var rootPath = _rootProvider.GetRootPath();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("Storage root path is not configured.");
        }

        var normalizedRoot = Path.GetFullPath(rootPath);

        if (_isLoaded && string.Equals(_currentRoot, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentRoot = normalizedRoot;
        Directory.CreateDirectory(rootPath);

        var metadataPath = GetMetadataPath(_currentRoot);
        if (!File.Exists(metadataPath))
        {
            _podcasts = new Dictionary<string, Podcast>(StringComparer.OrdinalIgnoreCase);
            _isLoaded = true;
            return;
        }

        await using var stream = File.OpenRead(metadataPath);
        try
        {
            var payload = await JsonSerializer.DeserializeAsync<PersistedState>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
            if (payload?.Podcasts is null)
            {
                _podcasts = new Dictionary<string, Podcast>(StringComparer.OrdinalIgnoreCase);
                _isLoaded = true;
                return;
            }

            _podcasts = payload.Podcasts
                .Select(p => ToPodcast(p, _currentRoot))
                .ToDictionary(p => p.Id, Clone, StringComparer.OrdinalIgnoreCase);
            _isLoaded = true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize podcasts metadata from {MetadataPath}", metadataPath);
            _podcasts = new Dictionary<string, Podcast>(StringComparer.OrdinalIgnoreCase);
            _isLoaded = true;
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_currentRoot is null)
        {
            throw new InvalidOperationException("Storage root has not been initialized.");
        }

        Directory.CreateDirectory(_currentRoot);
        var metadataPath = GetMetadataPath(_currentRoot);
        var state = new PersistedState
        {
            Podcasts = _podcasts.Values.Select(p => ToPersistedPodcast(p, _currentRoot)).ToList()
        };

        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, state, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string GetMetadataPath(string rootPath)
        => Path.Combine(rootPath, "podcasts.json");

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

    private Podcast ToPodcast(PersistedPodcast persisted, string rootPath)
    {
        var podcast = new Podcast(
            persisted.Id,
            new Uri(persisted.FeedUri, UriKind.Absolute),
            persisted.Title);

        podcast.UpdateMetadata(
            persisted.Title,
            persisted.Description,
            persisted.ArtworkUri is not null ? new Uri(persisted.ArtworkUri, UriKind.Absolute) : null,
            persisted.LastUpdated);

        if (persisted.Episodes is not null && persisted.Episodes.Count > 0)
        {
            var episodes = persisted.Episodes.Select(e => ToEpisode(e, rootPath));
            podcast.MergeEpisodes(episodes);
        }

        return podcast;
    }

    private Episode ToEpisode(PersistedEpisode persisted, string rootPath)
    {
        var episode = new Episode(
            persisted.Id,
            persisted.Title,
            new Uri(persisted.MediaUri, UriKind.Absolute));

        episode.UpdateMetadata(
            persisted.Summary,
            persisted.Duration,
            persisted.PublishedAt,
            persisted.ArtworkUri is not null ? new Uri(persisted.ArtworkUri, UriKind.Absolute) : null,
            persisted.EpisodeNumber);

        if (persisted.DownloadStatus.HasValue)
        {
            var localPath = ToAbsolutePath(rootPath, persisted.LocalFilePath);
            var artworkPath = ToAbsolutePath(rootPath, persisted.ArtworkFilePath);
            episode.SetDownloadState(persisted.DownloadStatus.Value, localPath, artworkPath);
        }

        episode.SetArtworkFilePath(ToAbsolutePath(rootPath, persisted.ArtworkFilePath));
        return episode;
    }

    private PersistedPodcast ToPersistedPodcast(Podcast podcast, string rootPath)
    {
        return new PersistedPodcast
        {
            Id = podcast.Id,
            FeedUri = podcast.FeedUri.ToString(),
            Title = podcast.Title,
            Description = podcast.Description,
            ArtworkUri = podcast.ArtworkUri?.ToString(),
            LastUpdated = podcast.LastUpdated,
            Episodes = podcast.Episodes.Select(e => ToPersistedEpisode(e, rootPath)).ToList()
        };
    }

    private PersistedEpisode ToPersistedEpisode(Episode episode, string rootPath)
    {
        return new PersistedEpisode
        {
            Id = episode.Id,
            Title = episode.Title,
            Summary = episode.Summary,
            ArtworkUri = episode.ArtworkUri?.ToString(),
            MediaUri = episode.MediaUri.ToString(),
            Duration = episode.Duration,
            PublishedAt = episode.PublishedAt,
            DownloadStatus = episode.DownloadStatus,
            LocalFilePath = ToRelativePath(rootPath, episode.LocalFilePath),
            EpisodeNumber = episode.EpisodeNumber,
            ArtworkFilePath = ToRelativePath(rootPath, episode.ArtworkFilePath)
        };
    }

    private sealed class PersistedState
    {
        public List<PersistedPodcast>? Podcasts { get; set; }
    }

    private sealed class PersistedPodcast
    {
        public string Id { get; set; } = string.Empty;

        public string FeedUri { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? ArtworkUri { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public List<PersistedEpisode> Episodes { get; set; } = new();
    }

    private sealed class PersistedEpisode
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Summary { get; set; }

        public string? ArtworkUri { get; set; }

        public string MediaUri { get; set; } = string.Empty;

        public TimeSpan? Duration { get; set; }

        public DateTimeOffset PublishedAt { get; set; }

        public DownloadStatus? DownloadStatus { get; set; }

        public string? LocalFilePath { get; set; }

        public int? EpisodeNumber { get; set; }

        public string? ArtworkFilePath { get; set; }
    }

    private static string? ToRelativePath(string rootPath, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathRooted(candidate))
        {
            return candidate;
        }

        var relative = Path.GetRelativePath(rootPath, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return candidate;
        }

        return NormalizeRelativeSeparators(relative);
    }

    private static string? ToAbsolutePath(string rootPath, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return candidate;
        }

        var normalized = candidate.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(rootPath, normalized);
        return Path.GetFullPath(fullPath);
    }

    private static string NormalizeRelativeSeparators(string relative)
    {
        var withForwardSlashes = relative.Replace(Path.DirectorySeparatorChar, '/');
        return withForwardSlashes.Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
