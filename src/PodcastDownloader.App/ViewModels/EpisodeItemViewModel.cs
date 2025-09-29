using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PodcastDownloader.App.Services;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.App.ViewModels;

public partial class EpisodeItemViewModel : ObservableObject
{
    private readonly IImageCache _imageCache;
    private Uri? _fallbackArtworkUri;
    private bool _hasExplicitArtwork;
    private CancellationTokenSource? _artworkCts;

    public EpisodeItemViewModel(Episode episode, IImageCache imageCache, Uri? fallbackArtworkUri)
    {
        _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
        _fallbackArtworkUri = fallbackArtworkUri;
        Id = episode.Id;
        MediaUri = episode.MediaUri;
        UpdateFrom(episode);
    }

    public string Id { get; }

    public Uri MediaUri { get; }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? summary;

    [ObservableProperty]
    private TimeSpan? duration;

    [ObservableProperty]
    private DateTimeOffset publishedAt;

    [ObservableProperty]
    private string? localFilePath;

    [ObservableProperty]
    private DownloadStatus downloadStatus;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private Uri? artworkUri;

    [ObservableProperty]
    private Bitmap? artwork;

    public bool IsDownloaded => DownloadStatus == DownloadStatus.Completed;

    public bool IsDownloading => DownloadStatus == DownloadStatus.InProgress;

    public string StatusText => DownloadStatus switch
    {
        DownloadStatus.NotStarted => "Not downloaded",
        DownloadStatus.InProgress => $"Downloading ({DownloadProgress:P0})",
        DownloadStatus.Completed => "Downloaded",
        DownloadStatus.Failed => "Failed",
        _ => "Unknown"
    };

    public void UpdateFrom(Episode episode)
    {
        Title = episode.Title;
        Summary = episode.Summary;
        Duration = episode.Duration;
        PublishedAt = episode.PublishedAt;
        LocalFilePath = episode.LocalFilePath;
        DownloadStatus = episode.DownloadStatus;
        DownloadProgress = episode.DownloadStatus == DownloadStatus.Completed ? 1 : 0;

        var localArtwork = TryCreateLocalArtworkUri(episode.ArtworkFilePath);

        _hasExplicitArtwork = localArtwork is not null || episode.ArtworkUri is not null;
        ArtworkUri = localArtwork ?? episode.ArtworkUri ?? _fallbackArtworkUri;
    }

    public void SetDownloadProgress(double progress)
    {
        DownloadStatus = DownloadStatus.InProgress;
        DownloadProgress = Math.Clamp(progress, 0d, 1d);
    }

    public void SetFallbackArtwork(Uri? fallback)
    {
        _fallbackArtworkUri = fallback;
        if (!_hasExplicitArtwork)
        {
            ArtworkUri = _fallbackArtworkUri;
        }
    }

    partial void OnDownloadStatusChanged(DownloadStatus value)
    {
        OnPropertyChanged(nameof(IsDownloaded));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnDownloadProgressChanged(double value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    partial void OnArtworkUriChanged(Uri? value)
    {
        _ = LoadArtworkAsync();
    }

    private async Task LoadArtworkAsync()
    {
        var previousCts = _artworkCts;
        if (previousCts is not null)
        {
            previousCts.Cancel();
            previousCts.Dispose();
        }

        if (ArtworkUri is null)
        {
            _artworkCts = null;
            Artwork = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _artworkCts = cts;

        try
        {
            var bitmap = await _imageCache.GetAsync(ArtworkUri, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                Artwork = bitmap;
            }
        }
        catch
        {
            // Ignore artwork load failures and leave previous artwork intact.
        }
        finally
        {
            if (ReferenceEquals(_artworkCts, cts))
            {
                _artworkCts = null;
            }

            cts.Dispose();
        }
    }

    private static Uri? TryCreateLocalArtworkUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            return new Uri(fullPath, UriKind.Absolute);
        }
        catch
        {
            return null;
        }
    }
}
