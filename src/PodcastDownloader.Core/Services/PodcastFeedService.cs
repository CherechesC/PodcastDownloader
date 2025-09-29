using System.Globalization;
using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Services;

public class PodcastFeedService : IPodcastFeedService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PodcastFeedService> _logger;

    public PodcastFeedService(HttpClient httpClient, ILogger<PodcastFeedService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Podcast> GetPodcastAsync(Uri feedUri, CancellationToken cancellationToken = default)
    {
        if (feedUri is null)
        {
            throw new ArgumentNullException(nameof(feedUri));
        }

        _logger.LogInformation("Fetching podcast feed {FeedUri}", feedUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = XmlReader.Create(contentStream, new XmlReaderSettings { Async = true });
        var feed = SyndicationFeed.Load(reader);

        if (feed is null)
        {
            throw new InvalidOperationException($"Unable to parse feed from '{feedUri}'.");
        }

        var title = feed.Title?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = feedUri.Host;
        }

        var podcast = new Podcast(Podcast.CreateId(feedUri), feedUri, title);
        podcast.UpdateMetadata(title, feed.Description?.Text?.Trim(), ResolveArtwork(feed), feed.LastUpdatedTime == default ? DateTimeOffset.UtcNow : feed.LastUpdatedTime);

        var episodes = feed.Items
            .Select(item => MapEpisode(item, feedUri))
            .Where(static e => e is not null)
            .Cast<Episode>()
            .ToList();

        podcast.MergeEpisodes(episodes);
        return podcast;
    }

    private static Uri? ResolveArtwork(SyndicationFeed feed)
    {
        if (feed.ImageUrl is not null)
        {
            return feed.ImageUrl;
        }

        var imageLink = feed.Links.FirstOrDefault(link => string.Equals(link.RelationshipType, "image", StringComparison.OrdinalIgnoreCase))
                        ?? feed.Links.FirstOrDefault(link => (link.MediaType?.StartsWith("image", StringComparison.OrdinalIgnoreCase)).GetValueOrDefault());
        return imageLink?.Uri;
    }

    private static Episode? MapEpisode(SyndicationItem item, Uri feedUri)
    {
        var enclosure = item.Links.FirstOrDefault(link => string.Equals(link.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase) && link.Uri is not null)
                        ?? item.Links.FirstOrDefault(link => link.Uri is not null);

        if (enclosure?.Uri is null)
        {
            return null;
        }

        var episodeUniqueKey = !string.IsNullOrWhiteSpace(item.Id)
            ? item.Id
            : FormattableString.Invariant($"{feedUri}:{enclosure.Uri}");

        var episodeId = CreateEpisodeId(episodeUniqueKey);
        var title = item.Title?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = enclosure.Uri.Segments.LastOrDefault()?.Trim('/') ?? enclosure.Uri.AbsolutePath;
        }

        var episode = new Episode(episodeId, title!, enclosure.Uri);
        var published = item.PublishDate != default ? item.PublishDate : item.LastUpdatedTime;
        var duration = TryParseDuration(item);
        var summary = item.Summary?.Text?.Trim();
        var artwork = ResolveEpisodeArtwork(item);

        var episodeNumber = TryParseEpisodeNumber(item);
        episode.UpdateMetadata(summary, duration, published == default ? DateTimeOffset.UtcNow : published, artwork, episodeNumber);
        return episode;
    }

    private static Uri? ResolveEpisodeArtwork(SyndicationItem item)
    {
        foreach (var extension in item.ElementExtensions)
        {
            try
            {
                if (string.Equals(extension.OuterName, "image", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(extension.OuterNamespace, "http://www.itunes.com/dtds/podcast-1.0.dtd", StringComparison.Ordinal))
                {
                    var element = extension.GetObject<XmlElement>();
                    var href = element.GetAttribute("href");
                    if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
                    {
                        return uri;
                    }
                }

                if (string.Equals(extension.OuterName, "thumbnail", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(extension.OuterNamespace, "http://search.yahoo.com/mrss/", StringComparison.OrdinalIgnoreCase))
                {
                    var element = extension.GetObject<XmlElement>();
                    var url = element.GetAttribute("url");
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        return uri;
                    }
                }
            }
            catch
            {
                // Ignore invalid extension shapes
            }
        }

        var imageLink = item.Links.FirstOrDefault(link => string.Equals(link.RelationshipType, "image", StringComparison.OrdinalIgnoreCase) && link.Uri is not null)
                        ?? item.Links.FirstOrDefault(link => (link.MediaType?.StartsWith("image", StringComparison.OrdinalIgnoreCase)).GetValueOrDefault());
        return imageLink?.Uri;
    }

    private static TimeSpan? TryParseDuration(SyndicationItem item)
    {
        try
        {
            var durationExtension = item.ElementExtensions.ReadElementExtensions<string>("duration", "http://www.itunes.com/dtds/podcast-1.0.dtd").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(durationExtension))
            {
                return null;
            }

            if (TimeSpan.TryParse(durationExtension, CultureInfo.InvariantCulture, out var duration))
            {
                return duration;
            }

            var parts = durationExtension.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes) && int.TryParse(parts[2], out var seconds))
            {
                return new TimeSpan(hours, minutes, seconds);
            }

            if (parts.Length == 2 && int.TryParse(parts[0], out minutes) && int.TryParse(parts[1], out var secs))
            {
                return new TimeSpan(0, minutes, secs);
            }

            if (int.TryParse(durationExtension, out var totalSeconds))
            {
                return TimeSpan.FromSeconds(totalSeconds);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string CreateEpisodeId(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static int? TryParseEpisodeNumber(SyndicationItem item)
    {
        try
        {
            var value = item.ElementExtensions.ReadElementExtensions<string>("episode", "http://www.itunes.com/dtds/podcast-1.0.dtd").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }
        }
        catch
        {
            // Ignore malformed extensions.
        }

        return null;
    }
}
