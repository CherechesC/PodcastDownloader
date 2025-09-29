using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Interfaces;

public interface IPodcastStorageService
{
    Task<string> GetPodcastDirectoryAsync(Podcast podcast, CancellationToken cancellationToken = default);

    Task<string> GetEpisodeDirectoryAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default);

    Task<string> GetEpisodeFilePathAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default);

    Task<string> GetEpisodeArtworkFilePathAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default);
}
