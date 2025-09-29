using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Services;

public class PodcastDownloadService : IPodcastDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly IPodcastStorageService _storageService;
    private readonly ILogger<PodcastDownloadService> _logger;

    public PodcastDownloadService(HttpClient httpClient, IPodcastStorageService storageService, ILogger<PodcastDownloadService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DownloadEpisodeAsync(Podcast podcast, Episode episode, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        if (episode is null)
        {
            throw new ArgumentNullException(nameof(episode));
        }

        var episodeDirectory = await _storageService.GetEpisodeDirectoryAsync(podcast, episode, cancellationToken).ConfigureAwait(false);
        var targetPath = await _storageService.GetEpisodeFilePathAsync(podcast, episode, cancellationToken).ConfigureAwait(false);
        var artworkFilePath = await DownloadEpisodeArtworkInternalAsync(podcast, episode, cancellationToken).ConfigureAwait(false);

        if (File.Exists(targetPath))
        {
            _logger.LogInformation("Episode {EpisodeTitle} already downloaded to {TargetPath}", episode.Title, targetPath);
            episode.MarkCompleted(targetPath, artworkFilePath ?? episode.ArtworkFilePath);
            progress?.Report(1);
            return;
        }

        Directory.CreateDirectory(episodeDirectory);

        using var request = new HttpRequestMessage(HttpMethod.Get, episode.MediaUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = File.Create(targetPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;

            if (contentLength.HasValue && contentLength.Value > 0)
            {
                progress?.Report(Math.Min(1d, totalRead / (double)contentLength.Value));
            }
        }

        if (!contentLength.HasValue)
        {
            progress?.Report(1);
        }

        episode.MarkCompleted(targetPath, artworkFilePath ?? episode.ArtworkFilePath);
        _logger.LogInformation("Episode {EpisodeTitle} downloaded successfully", episode.Title);
    }

    public Task<string?> DownloadEpisodeArtworkAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
    {
        if (podcast is null)
        {
            throw new ArgumentNullException(nameof(podcast));
        }

        if (episode is null)
        {
            throw new ArgumentNullException(nameof(episode));
        }

        return DownloadEpisodeArtworkInternalAsync(podcast, episode, cancellationToken);
    }

    private async Task<string?> DownloadEpisodeArtworkInternalAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken)
    {
        if (episode.ArtworkUri is null)
        {
            return episode.ArtworkFilePath;
        }

        var artworkPath = await _storageService.GetEpisodeArtworkFilePathAsync(podcast, episode, cancellationToken).ConfigureAwait(false);

        if (File.Exists(artworkPath))
        {
            episode.SetArtworkFilePath(artworkPath);
            return artworkPath;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(artworkPath)!);
            using var request = new HttpRequestMessage(HttpMethod.Get, episode.ArtworkUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(artworkPath);
            await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

            episode.SetArtworkFilePath(artworkPath);
            return artworkPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download artwork for episode {EpisodeTitle}", episode.Title);
            return null;
        }
    }
}
