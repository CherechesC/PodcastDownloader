using System.Collections.Concurrent;

namespace PodcastDownloader.Core.Models;

public class Podcast
{
    private readonly List<Episode> _episodes = new();

    public Podcast(string id, Uri feedUri, string title)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Podcast id cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Podcast title cannot be null or whitespace.", nameof(title));
        }

        Id = id;
        FeedUri = feedUri ?? throw new ArgumentNullException(nameof(feedUri));
        Title = title;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    public string Id { get; }

    public Uri FeedUri { get; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public Uri? ArtworkUri { get; private set; }

    public DateTimeOffset LastUpdated { get; private set; }

    public IReadOnlyList<Episode> Episodes => _episodes;

    public static string CreateId(Uri feedUri)
    {
        using var sha = SHA256.Create();
        var normalized = feedUri.ToString().Trim().ToLowerInvariant();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    public void UpdateMetadata(string title, string? description, Uri? artworkUri, DateTimeOffset lastUpdated)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }

        Description = description;
        ArtworkUri = artworkUri;
        LastUpdated = lastUpdated;
    }

    public void ReplaceEpisodes(IEnumerable<Episode> episodes)
    {
        var incomingEpisodes = episodes?.ToDictionary(e => e.Id, StringComparer.Ordinal) ?? new Dictionary<string, Episode>();
        var updated = new List<Episode>(incomingEpisodes.Count);

        foreach (var episode in incomingEpisodes.Values.OrderByDescending(e => e.PublishedAt))
        {
            updated.Add(episode);
        }

        _episodes.Clear();
        _episodes.AddRange(updated);
    }

    public Episode? GetEpisodeById(string episodeId)
    {
        return _episodes.FirstOrDefault(e => string.Equals(e.Id, episodeId, StringComparison.Ordinal));
    }

    public void MergeEpisodes(IEnumerable<Episode> episodes)
    {
        foreach (var episode in episodes)
        {
            var existing = GetEpisodeById(episode.Id);
            if (existing is null)
            {
                _episodes.Add(episode);
            }
            else
            {
                existing.MergeFrom(episode);
            }
        }

        _episodes.Sort((a, b) => Comparer<DateTimeOffset>.Default.Compare(b.PublishedAt, a.PublishedAt));
    }
}
