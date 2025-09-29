using System.ComponentModel;

namespace PodcastDownloader.Core.Models;

public class Episode
{
    public Episode(string id, string title, Uri mediaUri)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Episode id cannot be null or whitespace.", nameof(id));
        }

        Id = id;
        Title = title;
        MediaUri = mediaUri ?? throw new ArgumentNullException(nameof(mediaUri));
    }

    public string Id { get; }

    public string Title { get; private set; }

    public string? Summary { get; private set; }

    public Uri? ArtworkUri { get; private set; }

    public Uri MediaUri { get; }

    public TimeSpan? Duration { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public string? LocalFilePath { get; set; }

    public string? ArtworkFilePath { get; private set; }

    public int? EpisodeNumber { get; private set; }

    public DownloadStatus DownloadStatus { get; private set; } = DownloadStatus.NotStarted;

    public bool IsDownloaded => DownloadStatus == DownloadStatus.Completed && !string.IsNullOrWhiteSpace(LocalFilePath);

    public void MarkInProgress() => DownloadStatus = DownloadStatus.InProgress;

    public void MarkCompleted(string localFilePath, string? artworkFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(localFilePath))
        {
            throw new ArgumentException("Local file path cannot be null or whitespace.", nameof(localFilePath));
        }

        LocalFilePath = localFilePath;
        SetArtworkFilePath(artworkFilePath);

        DownloadStatus = DownloadStatus.Completed;
    }

    public void MarkFailed()
    {
        DownloadStatus = DownloadStatus.Failed;
    }

    public void UpdateMetadata(string? summary, TimeSpan? duration, DateTimeOffset publishedAt, Uri? artworkUri = null, int? episodeNumber = null)
    {
        Summary = summary;
        Duration = duration;
        PublishedAt = publishedAt;
        ArtworkUri = artworkUri ?? ArtworkUri;
        EpisodeNumber = episodeNumber ?? EpisodeNumber;
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be null or whitespace.", nameof(title));
        }

        Title = title;
    }

    public void MergeFrom(Episode other)
    {
        if (!Id.Equals(other.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Cannot merge episodes with different ids.");
        }

        Title = other.Title;
        Summary = other.Summary;
        Duration = other.Duration;
        PublishedAt = other.PublishedAt;
        ArtworkUri = other.ArtworkUri ?? ArtworkUri;
        EpisodeNumber = other.EpisodeNumber ?? EpisodeNumber;

        if (!string.IsNullOrWhiteSpace(other.LocalFilePath))
        {
            LocalFilePath = other.LocalFilePath;
        }

        if (!string.IsNullOrWhiteSpace(other.ArtworkFilePath))
        {
            ArtworkFilePath = other.ArtworkFilePath;
        }

        DownloadStatus = other.DownloadStatus;
    }

    internal void SetDownloadState(DownloadStatus status, string? localFilePath, string? artworkFilePath = null)
    {
        DownloadStatus = status;
        LocalFilePath = localFilePath;
        SetArtworkFilePath(artworkFilePath);
    }

    public void SetArtworkFilePath(string? artworkFilePath)
    {
        if (!string.IsNullOrWhiteSpace(artworkFilePath))
        {
            ArtworkFilePath = artworkFilePath;
        }
    }
}
