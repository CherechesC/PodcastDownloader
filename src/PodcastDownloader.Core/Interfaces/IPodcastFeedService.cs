using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Interfaces;

public interface IPodcastFeedService
{
    Task<Podcast> GetPodcastAsync(Uri feedUri, CancellationToken cancellationToken = default);
}
