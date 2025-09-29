using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace PodcastDownloader.App.Services;

public interface IImageCache
{
    Task<Bitmap?> GetAsync(Uri? uri, CancellationToken cancellationToken = default);
}
