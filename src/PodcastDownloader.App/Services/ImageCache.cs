using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PodcastDownloader.App.Services;

public class ImageCache : IImageCache
{
    private const string ClientName = "podcast-artwork";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<Uri, Lazy<Task<Bitmap?>>> _cache = new();

    public ImageCache(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<Bitmap?> GetAsync(Uri? uri, CancellationToken cancellationToken = default)
    {
        if (uri is null)
        {
            return null;
        }

        var lazy = _cache.GetOrAdd(uri, key => new Lazy<Task<Bitmap?>>(() => LoadBitmapAsync(key, CancellationToken.None)));

        var bitmap = await lazy.Value;
        if (bitmap is null)
        {
            _cache.TryRemove(uri, out _);
        }

        return bitmap;
    }

    private async Task<Bitmap?> LoadBitmapAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient(ClientName);
                await using var responseStream = await client.GetStreamAsync(uri, cancellationToken).ConfigureAwait(false);
                using var memory = new MemoryStream();
                await responseStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                memory.Position = 0;
                return Bitmap.DecodeToWidth(memory, 256);
            }

            if (uri.IsFile)
            {
                using var fileStream = File.OpenRead(uri.LocalPath);
                return Bitmap.DecodeToWidth(fileStream, 256);
            }

            if (AssetLoader.Exists(uri))
            {
                using var assetStream = AssetLoader.Open(uri);
                return Bitmap.DecodeToWidth(assetStream, 256);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
