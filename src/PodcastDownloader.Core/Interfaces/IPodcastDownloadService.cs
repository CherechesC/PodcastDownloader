using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Interfaces;

public interface IPodcastDownloadService
{
    Task DownloadEpisodeAsync(Podcast podcast, Episode episode, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<string?> DownloadEpisodeArtworkAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default);
}
