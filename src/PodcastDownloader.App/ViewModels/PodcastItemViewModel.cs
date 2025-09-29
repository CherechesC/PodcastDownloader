using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PodcastDownloader.App.Services;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.App.ViewModels;

public partial class PodcastItemViewModel : ObservableObject
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(7);

    private readonly IImageCache _imageCache;
    private CancellationTokenSource? _artworkCts;

    public PodcastItemViewModel(Podcast podcast, IImageCache imageCache)
    {
        _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
        Id = podcast.Id;
        FeedUri = podcast.FeedUri;
        Episodes = new ObservableCollection<EpisodeItemViewModel>();
        UpdateFrom(podcast);
    }

    public string Id { get; }

    public Uri FeedUri { get; }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private Uri? artworkUri;

    [ObservableProperty]
    private DateTimeOffset lastUpdated;

    [ObservableProperty]
    private Bitmap? artwork;

    [ObservableProperty]
    private int totalEpisodes;

    [ObservableProperty]
    private int downloadedEpisodes;

    public ObservableCollection<EpisodeItemViewModel> Episodes { get; }

    public bool AllEpisodesDownloaded => TotalEpisodes > 0 && DownloadedEpisodes == TotalEpisodes;

    public bool HasPendingDownloads => TotalEpisodes > DownloadedEpisodes;

    public int PendingDownloadCount => Math.Max(0, TotalEpisodes - DownloadedEpisodes);

    public string DownloadSummary
        => TotalEpisodes switch
        {
            0 => "No episodes yet",
            _ when AllEpisodesDownloaded => "All episodes downloaded",
            _ => string.Format(CultureInfo.CurrentCulture, "{0}/{1} downloaded", DownloadedEpisodes, TotalEpisodes)
        };

    public bool IsStale
    {
        get
        {
            if (LastUpdated == default)
            {
                return true;
            }

            return DateTimeOffset.UtcNow - LastUpdated > StaleThreshold;
        }
    }

    public string SyncStatusText
    {
        get
        {
            if (LastUpdated == default)
            {
                return "Last updated: unknown";
            }

            var lastLocal = LastUpdated.ToLocalTime();
            var relative = GetRelativeTime(lastLocal, DateTimeOffset.Now);
            var needsRefreshSuffix = IsStale ? " · Needs refresh" : string.Empty;
            return string.Format(CultureInfo.CurrentCulture, "Last updated {0}{1}", relative, needsRefreshSuffix);
        }
    }

    public void UpdateFrom(Podcast podcast)
    {
        Title = podcast.Title;
        Description = podcast.Description;
        ArtworkUri = podcast.ArtworkUri;
        LastUpdated = podcast.LastUpdated;

        var existing = Episodes.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var episode in podcast.Episodes)
        {
            if (existing.TryGetValue(episode.Id, out var viewModel))
            {
                viewModel.UpdateFrom(episode);
                viewModel.SetFallbackArtwork(podcast.ArtworkUri);
                existing.Remove(episode.Id);
            }
            else
            {
                var episodeViewModel = new EpisodeItemViewModel(episode, _imageCache, podcast.ArtworkUri);
                AttachEpisodeHandlers(episodeViewModel);
                Episodes.Add(episodeViewModel);
            }
        }

        foreach (var leftover in existing.Values)
        {
            DetachEpisodeHandlers(leftover);
            Episodes.Remove(leftover);
        }

        SortEpisodes();
        UpdateAggregateEpisodeCounts();
    }

    partial void OnArtworkUriChanged(Uri? value)
    {
        _ = LoadArtworkAsync();
    }

    partial void OnLastUpdatedChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(SyncStatusText));
    }

    partial void OnTotalEpisodesChanged(int value)
    {
        OnAggregateEpisodeStateChanged();
    }

    partial void OnDownloadedEpisodesChanged(int value)
    {
        OnAggregateEpisodeStateChanged();
    }

    private void SortEpisodes()
    {
        var ordered = Episodes.OrderByDescending(e => e.PublishedAt).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var index = Episodes.IndexOf(current);
            if (index != i)
            {
                Episodes.Move(index, i);
            }
        }
    }

    private void AttachEpisodeHandlers(EpisodeItemViewModel episode)
    {
        episode.PropertyChanged += OnEpisodePropertyChanged;
    }

    private void DetachEpisodeHandlers(EpisodeItemViewModel episode)
    {
        episode.PropertyChanged -= OnEpisodePropertyChanged;
    }

    private void OnEpisodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EpisodeItemViewModel.IsDownloaded) or nameof(EpisodeItemViewModel.DownloadStatus))
        {
            UpdateAggregateEpisodeCounts();
        }
    }

    private void UpdateAggregateEpisodeCounts()
    {
        TotalEpisodes = Episodes.Count;
        DownloadedEpisodes = Episodes.Count(e => e.IsDownloaded);
    }

    private void OnAggregateEpisodeStateChanged()
    {
        OnPropertyChanged(nameof(AllEpisodesDownloaded));
        OnPropertyChanged(nameof(HasPendingDownloads));
        OnPropertyChanged(nameof(PendingDownloadCount));
        OnPropertyChanged(nameof(DownloadSummary));
    }

    private async Task LoadArtworkAsync()
    {
        _artworkCts?.Cancel();
        var cts = new CancellationTokenSource();
        _artworkCts = cts;

        try
        {
            if (ArtworkUri is null)
            {
                Artwork = null;
                return;
            }

            var bitmap = await _imageCache.GetAsync(ArtworkUri, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                Artwork = bitmap;
            }
        }
        catch
        {
            // Ignore artwork load failures.
        }
        finally
        {
            if (ReferenceEquals(_artworkCts, cts))
            {
                _artworkCts.Dispose();
                _artworkCts = null;
            }
            else
            {
                cts.Dispose();
            }
        }
    }

    private static string GetRelativeTime(DateTimeOffset lastUpdate, DateTimeOffset now)
    {
        var delta = now - lastUpdate;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta <= TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = (int)Math.Floor(delta.TotalMinutes);
            return minutes == 1 ? "1 minute ago" : string.Format(CultureInfo.CurrentCulture, "{0} minutes ago", minutes);
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = (int)Math.Floor(delta.TotalHours);
            return hours == 1 ? "1 hour ago" : string.Format(CultureInfo.CurrentCulture, "{0} hours ago", hours);
        }

        if (delta < TimeSpan.FromDays(7))
        {
            var days = (int)Math.Floor(delta.TotalDays);
            return days == 1 ? "1 day ago" : string.Format(CultureInfo.CurrentCulture, "{0} days ago", days);
        }

        return lastUpdate.ToString("g", CultureInfo.CurrentCulture);
    }
}
