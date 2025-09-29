using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.Core.Services;

public sealed class ApplePodcastDiscoveryService : IPodcastDiscoveryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplePodcastDiscoveryService> _logger;

    public ApplePodcastDiscoveryService(HttpClient httpClient, ILogger<ApplePodcastDiscoveryService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PodcastSearchResult>();
        }

        var requestUri = $"search?media=podcast&entity=podcast&limit=25&term={Uri.EscapeDataString(query)}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Apple podcast search failed with status code {StatusCode}", response.StatusCode);
                return Array.Empty<PodcastSearchResult>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("results", out var resultsElement))
            {
                return Array.Empty<PodcastSearchResult>();
            }

            var results = new List<PodcastSearchResult>();
            foreach (var item in resultsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("feedUrl", out var feedUrlElement))
                {
                    continue;
                }

                var feedUrl = feedUrlElement.GetString();
                if (string.IsNullOrWhiteSpace(feedUrl) || !Uri.TryCreate(feedUrl, UriKind.Absolute, out var feedUri))
                {
                    continue;
                }

                var title = TryGetString(item, "collectionName") ?? TryGetString(item, "trackName") ?? feedUri.Host;
                var author = TryGetString(item, "artistName");
                var description = TryGetString(item, "description") ?? TryGetString(item, "collectionCensoredName");
                var genre = TryGetString(item, "primaryGenreName");
                var artwork = TryGetString(item, "artworkUrl600") ?? TryGetString(item, "artworkUrl100");
                Uri? artworkUri = null;
                if (!string.IsNullOrWhiteSpace(artwork) && Uri.TryCreate(artwork, UriKind.Absolute, out var parsedArtwork))
                {
                    artworkUri = parsedArtwork;
                }

                results.Add(new PodcastSearchResult(title, feedUri, author, description, artworkUri, genre));
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apple podcast search failed");
            return Array.Empty<PodcastSearchResult>();
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString();
        }

        return null;
    }
}
