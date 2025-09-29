using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Storage;

public class FileSystemStorageService : IPodcastStorageService
{
    private readonly IStorageRootProvider _rootProvider;

    public FileSystemStorageService(IStorageRootProvider rootProvider)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
    }

    public Task<string> GetPodcastDirectoryAsync(Podcast podcast, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        var rootPath = _rootProvider.GetRootPath();
        var safeFolder = SanitizeForPath(string.IsNullOrWhiteSpace(podcast.Title) ? podcast.Id : podcast.Title);
        var target = Path.Combine(rootPath, safeFolder);
        Directory.CreateDirectory(target);
        return Task.FromResult(target);
    }

    public async Task<string> GetEpisodeDirectoryAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        if (episode is null)
        {
            throw new ArgumentNullException(nameof(episode));
        }

        var podcastDirectory = await GetPodcastDirectoryAsync(podcast, cancellationToken).ConfigureAwait(false);
        var episodeFolderName = GetEpisodeFolderName(podcast, episode);

        var episodeDirectory = Path.Combine(podcastDirectory, episodeFolderName);
        Directory.CreateDirectory(episodeDirectory);
        return episodeDirectory;
    }

    public async Task<string> GetEpisodeFilePathAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
    {
        var episodeDirectory = await GetEpisodeDirectoryAsync(podcast, episode, cancellationToken).ConfigureAwait(false);

        var extension = Path.GetExtension(episode.MediaUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp3";
        }

        var mediaFileName = Path.GetFileNameWithoutExtension(episode.MediaUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(mediaFileName))
        {
            mediaFileName = episode.Id;
        }

        mediaFileName = SanitizeForPath(mediaFileName);

        var fileName = $"{mediaFileName}{extension}";
        return Path.Combine(episodeDirectory, fileName);
    }

    public async Task<string> GetEpisodeArtworkFilePathAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        if (episode is null)
        {
            throw new ArgumentNullException(nameof(episode));
        }

        var podcastDirectory = await GetPodcastDirectoryAsync(podcast, cancellationToken).ConfigureAwait(false);
        var artworkDirectory = Path.Combine(podcastDirectory, "Art");
        Directory.CreateDirectory(artworkDirectory);

        var extension = episode.ArtworkUri is not null
            ? Path.GetExtension(episode.ArtworkUri.AbsolutePath)
            : string.Empty;

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var episodeNumberSegment = episode.EpisodeNumber.HasValue ? $"EP{episode.EpisodeNumber.Value:D3}" : "EP000";
        var titleSegment = SanitizeForPath(episode.Title);
        var fileName = $"{episodeNumberSegment}-{titleSegment}{extension}";
        return Path.Combine(artworkDirectory, fileName);
    }

    private static string GetEpisodeFolderName(Podcast podcast, Episode episode)
    {
        var titleSegment = SanitizeForPath(episode.Title);

        if (episode.EpisodeNumber.HasValue)
        {
            return $"EP{episode.EpisodeNumber.Value:D3}-{titleSegment}";
        }

        if (episode.PublishedAt != default)
        {
            return $"{episode.PublishedAt:yyyyMMddHHmmss}-{titleSegment}";
        }

        return $"{episode.Id}-{titleSegment}";
    }

    private static string SanitizeForPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value.Trim();
    }
}
