using System;

namespace PodcastDownloader.Core.Models;

public sealed record PodcastSearchResult(
    string Title,
    Uri FeedUri,
    string? Author,
    string? Description,
    Uri? ArtworkUri,
    string? Genre);
