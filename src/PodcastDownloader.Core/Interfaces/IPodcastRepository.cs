using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Interfaces;

public interface IPodcastRepository
{
    Task<IReadOnlyList<Podcast>> ListAsync(CancellationToken cancellationToken = default);

    Task<Podcast?> GetAsync(string podcastId, CancellationToken cancellationToken = default);

    Task UpsertAsync(Podcast podcast, CancellationToken cancellationToken = default);

    Task RemoveAsync(string podcastId, CancellationToken cancellationToken = default);
}
