using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PodcastDownloader.Core.Services;

namespace PodcastDownloader.Tests;

public class ApplePodcastDiscoveryServiceTests
{
    [Fact]
    public async Task SearchAsync_ParsesResultsFromResponse()
    {
        const string payload = """
        {
          "resultCount": 1,
          "results": [
            {
              "collectionName": "Science Weekly",
              "artistName": "Example Author",
              "feedUrl": "https://example.com/feed.xml",
              "description": "An in-depth look at science.",
              "primaryGenreName": "Science",
              "artworkUrl600": "https://example.com/art.jpg"
            }
          ]
        }
        """;

        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            request.RequestUri.Should().Be(new Uri("https://itunes.apple.com/search?media=podcast&entity=podcast&limit=25&term=science"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://itunes.apple.com/")
        };

        var logger = NullLogger<ApplePodcastDiscoveryService>.Instance;
        var service = new ApplePodcastDiscoveryService(client, logger);

        var results = await service.SearchAsync("science");

        results.Should().HaveCount(1);
        var result = results[0];
        result.Title.Should().Be("Science Weekly");
        result.Author.Should().Be("Example Author");
        result.Description.Should().Be("An in-depth look at science.");
        result.Genre.Should().Be("Science");
        result.FeedUri.Should().Be(new Uri("https://example.com/feed.xml"));
        result.ArtworkUri.Should().Be(new Uri("https://example.com/art.jpg"));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
