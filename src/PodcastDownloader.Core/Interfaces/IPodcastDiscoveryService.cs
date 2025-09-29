using System.Threading;
using System.Threading.Tasks;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Interfaces;

public interface IPodcastDiscoveryService
{
    Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
