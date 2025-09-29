using System.Threading;
using System.Threading.Tasks;

namespace PodcastDownloader.App.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
