using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodcastDownloader.Core.Models;

namespace PodcastDownloader.App.ViewModels;

public partial class PodcastSearchResultViewModel : ObservableObject
{
    public PodcastSearchResultViewModel(PodcastSearchResult result, IAsyncRelayCommand<PodcastSearchResultViewModel> subscribeCommand)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        SubscribeCommand = subscribeCommand ?? throw new ArgumentNullException(nameof(subscribeCommand));
    }

    public PodcastSearchResult Result { get; }

    public IAsyncRelayCommand<PodcastSearchResultViewModel> SubscribeCommand { get; }

    public string Title => Result.Title;

    public string FeedUrl => Result.FeedUri.ToString();

    public Uri FeedUri => Result.FeedUri;

    public string? Author => Result.Author;

    public string? Description => Result.Description;

    public Uri? ArtworkUri => Result.ArtworkUri;

    public string? Genre => Result.Genre;
}
