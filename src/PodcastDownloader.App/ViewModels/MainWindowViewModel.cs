using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PodcastDownloader.App.Services;
using Microsoft.Extensions.Logging;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Models;
using PodcastDownloader.Core.Services;

namespace PodcastDownloader.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PodcastManager _podcastManager;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IImageCache _imageCache;
    private readonly IStorageRootProvider _storageRootProvider;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IPodcastDiscoveryService _podcastDiscoveryService;
    private bool _isInitialized;

    public MainWindowViewModel(
        PodcastManager podcastManager,
        ILogger<MainWindowViewModel> logger,
        IImageCache imageCache,
        IStorageRootProvider storageRootProvider,
        IFolderPickerService folderPickerService,
        IPodcastDiscoveryService podcastDiscoveryService)
    {
        _podcastManager = podcastManager ?? throw new ArgumentNullException(nameof(podcastManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
        _storageRootProvider = storageRootProvider ?? throw new ArgumentNullException(nameof(storageRootProvider));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _podcastDiscoveryService = podcastDiscoveryService ?? throw new ArgumentNullException(nameof(podcastDiscoveryService));

        Podcasts = new ObservableCollection<PodcastItemViewModel>();
        SearchResults = new ObservableCollection<PodcastSearchResultViewModel>();

        SubscribeCommand = new AsyncRelayCommand(SubscribeAsync, CanSubscribe);
        RefreshCommand = new AsyncRelayCommand(RefreshSelectedPodcastAsync, CanRefresh);
        DownloadCommand = new AsyncRelayCommand(DownloadSelectedEpisodeAsync, CanDownload);
        RemoveCommand = new AsyncRelayCommand(RemoveSelectedPodcastAsync, CanRefresh);
        ChangeDownloadFolderCommand = new AsyncRelayCommand(ChangeDownloadFolderAsync);
        DownloadAllCommand = new AsyncRelayCommand(DownloadAllEpisodesAsync, CanDownloadAll);
        SearchCommand = new AsyncRelayCommand(SearchAsync, CanSearch);
        SubscribeToSearchResultCommand = new AsyncRelayCommand<PodcastSearchResultViewModel>(SubscribeFromSearchResultAsync, CanSubscribeFromSearchResult);
    RefreshAllPodcastsCommand = new AsyncRelayCommand(RefreshAllPodcastsAsync, CanRefreshAll);
        ClearSearchResultsCommand = new RelayCommand(ClearSearchResults, CanClearSearchResults);

        SearchResults.CollectionChanged += (_, _) => ClearSearchResultsCommand.NotifyCanExecuteChanged();

        StatusMessage = "Enter or paste a podcast feed URL to subscribe.";
    }

    public ObservableCollection<PodcastItemViewModel> Podcasts { get; }
    public ObservableCollection<PodcastSearchResultViewModel> SearchResults { get; }

    [ObservableProperty]
    private PodcastItemViewModel? selectedPodcast;

    [ObservableProperty]
    private EpisodeItemViewModel? selectedEpisode;

    [ObservableProperty]
    private string? feedUrl;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string downloadRootPath = string.Empty;

    [ObservableProperty]
    private string? searchTerm;

    [ObservableProperty]
    private PodcastSearchResultViewModel? selectedSearchResult;

    public IAsyncRelayCommand SubscribeCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand DownloadCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand ChangeDownloadFolderCommand { get; }
    public IAsyncRelayCommand DownloadAllCommand { get; }
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand<PodcastSearchResultViewModel> SubscribeToSearchResultCommand { get; }
    public IAsyncRelayCommand RefreshAllPodcastsCommand { get; }
    public IRelayCommand ClearSearchResultsCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        DownloadRootPath = _storageRootProvider.GetRootPath();
        await RunBusyAsync(SynchronizeAsync, cancellationToken);
        _isInitialized = true;
    }

    private bool CanSubscribe()
    {
        return !IsBusy && Uri.TryCreate(FeedUrl, UriKind.Absolute, out _);
    }

    private bool CanSearch()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(SearchTerm);
    }

    private bool CanRefresh()
    {
        return !IsBusy && SelectedPodcast is not null;
    }

    private bool CanDownload()
    {
        return !IsBusy && SelectedPodcast is not null && SelectedEpisode is not null;
    }

    private bool CanDownloadAll()
    {
        return !IsBusy && SelectedPodcast is not null && SelectedPodcast.Episodes.Count > 0;
    }

    private bool CanRefreshAll()
    {
        return !IsBusy && Podcasts.Count > 0;
    }

    private bool CanSubscribeFromSearchResult(PodcastSearchResultViewModel? result)
    {
        return !IsBusy && result is not null;
    }

    private bool CanClearSearchResults()
    {
        return SearchResults.Count > 0 || !string.IsNullOrWhiteSpace(SearchTerm);
    }

    private async Task SubscribeAsync()
    {
        if (!Uri.TryCreate(FeedUrl, UriKind.Absolute, out var feedUri))
        {
            StatusMessage = "Please enter a valid podcast feed URL.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            var podcast = await _podcastManager.SubscribeAsync(feedUri, ct);
            var viewModel = UpsertPodcastViewModel(podcast);
            SelectedPodcast = viewModel;
            FeedUrl = string.Empty;
            StatusMessage = $"Subscribed to '{podcast.Title}'.";
        });
    }

    private async Task RefreshSelectedPodcastAsync()
    {
        if (SelectedPodcast is null)
        {
            return;
        }

        var podcastId = SelectedPodcast.Id;
        await RunBusyAsync(async ct =>
        {
            var refreshed = await _podcastManager.RefreshAsync(podcastId, ct);
            var viewModel = UpsertPodcastViewModel(refreshed);
            SelectedPodcast = viewModel;
            StatusMessage = $"Refreshed '{refreshed.Title}'.";
        });
    }

    private async Task SearchAsync()
    {
        var term = SearchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            StatusMessage = "Enter a search term to discover podcasts.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            var results = await _podcastDiscoveryService.SearchAsync(term, ct);

            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(new PodcastSearchResultViewModel(result, SubscribeToSearchResultCommand));
            }

            SelectedSearchResult = SearchResults.FirstOrDefault();
            StatusMessage = SearchResults.Count > 0
                ? $"Found {SearchResults.Count} podcast{(SearchResults.Count == 1 ? string.Empty : "s")} for '{term}'."
                : $"No podcasts found for '{term}'.";
            ClearSearchResultsCommand.NotifyCanExecuteChanged();
        });
    }

    private async Task SubscribeFromSearchResultAsync(PodcastSearchResultViewModel? result)
    {
        if (result is null)
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            var podcast = await _podcastManager.SubscribeAsync(result.FeedUri, ct);
            var viewModel = UpsertPodcastViewModel(podcast);
            SelectedPodcast = viewModel;
            SelectedEpisode = viewModel.Episodes.FirstOrDefault();
            StatusMessage = $"Subscribed to '{podcast.Title}'.";
        });
    }

    private async Task RefreshAllPodcastsAsync()
    {
        if (Podcasts.Count == 0)
        {
            StatusMessage = "No podcasts to refresh.";
            return;
        }

        var podcastIds = Podcasts.Select(p => p.Id).ToList();

        await RunBusyAsync(async ct =>
        {
            var total = podcastIds.Count;
            var processed = 0;

            foreach (var podcastId in podcastIds)
            {
                processed++;
                StatusMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, "Refreshing podcasts... {0}/{1}", processed, total);

                var refreshed = await _podcastManager.RefreshAsync(podcastId, ct);
                var viewModel = UpsertPodcastViewModel(refreshed);

                if (SelectedPodcast?.Id == podcastId)
                {
                    SelectedPodcast = viewModel;
                }
            }

            StatusMessage = total == 1
                ? "Refreshed 1 podcast."
                : string.Format(System.Globalization.CultureInfo.CurrentCulture, "Refreshed {0} podcasts.", total);

            var updated = await _podcastManager.GetPodcastsAsync(ct);
            foreach (var podcast in updated)
            {
                UpsertPodcastViewModel(podcast);
            }
        });
    }

    private void ClearSearchResults()
    {
        SearchResults.Clear();
        SelectedSearchResult = null;
        SearchTerm = string.Empty;
        StatusMessage = "Cleared search results.";
    }

    private async Task DownloadSelectedEpisodeAsync()
    {
        if (SelectedPodcast is null || SelectedEpisode is null)
        {
            return;
        }

        var podcastId = SelectedPodcast.Id;
        var episodeId = SelectedEpisode.Id;
        var episodeViewModel = SelectedEpisode;

        await RunBusyAsync(async ct =>
        {
            var progress = new Progress<double>(value => episodeViewModel.SetDownloadProgress(value));
            await _podcastManager.DownloadEpisodeAsync(podcastId, episodeId, progress, ct);

            var updated = await _podcastManager.GetPodcastsAsync(ct);
            foreach (var podcast in updated)
            {
                UpsertPodcastViewModel(podcast);
            }

            var matchingPodcast = Podcasts.FirstOrDefault(p => string.Equals(p.Id, podcastId, StringComparison.OrdinalIgnoreCase));
            if (matchingPodcast is not null)
            {
                SelectedPodcast = matchingPodcast;
                SelectedEpisode = matchingPodcast.Episodes.FirstOrDefault(e => string.Equals(e.Id, episodeId, StringComparison.OrdinalIgnoreCase));
            }

            StatusMessage = "Episode downloaded.";
        });
    }

    private async Task DownloadAllEpisodesAsync()
    {
        if (SelectedPodcast is null)
        {
            return;
        }

        var podcastId = SelectedPodcast.Id;

        await RunBusyAsync(async ct =>
        {
            var overallProgress = new Progress<double>(value =>
            {
                StatusMessage = $"Downloading all episodes... {value:P0}";
            });

            await _podcastManager.DownloadAllEpisodesAsync(podcastId, 20, overallProgress, ct);

            var updated = await _podcastManager.GetPodcastsAsync(ct);
            foreach (var podcast in updated)
            {
                UpsertPodcastViewModel(podcast);
            }

            var matchingPodcast = Podcasts.FirstOrDefault(p => string.Equals(p.Id, podcastId, StringComparison.OrdinalIgnoreCase));
            if (matchingPodcast is not null)
            {
                SelectedPodcast = matchingPodcast;
                SelectedEpisode = matchingPodcast.Episodes.FirstOrDefault();
            }

            StatusMessage = "All episodes downloaded.";
        });
    }

    private async Task RemoveSelectedPodcastAsync()
    {
        if (SelectedPodcast is null)
        {
            return;
        }

        var selected = SelectedPodcast;
        if (selected is null)
        {
            return;
        }

        var podcastId = selected.Id;
        await RunBusyAsync(async ct =>
        {
            await _podcastManager.RemoveAsync(podcastId, ct);
            var index = Podcasts.IndexOf(selected);
            Podcasts.Remove(selected);
            StatusMessage = "Podcast removed.";
            if (Podcasts.Count == 0)
            {
                SelectedPodcast = null;
                SelectedEpisode = null;
            }
            else
            {
                SelectedPodcast = Podcasts[Math.Clamp(index, 0, Podcasts.Count - 1)];
            }
        });
    }

    private async Task ChangeDownloadFolderAsync()
    {
        var selectedPath = await _folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            StatusMessage = "Download folder selection cancelled.";
            return;
        }

        _storageRootProvider.SetRootPath(selectedPath);
        DownloadRootPath = _storageRootProvider.GetRootPath();

        await RunBusyAsync(async ct =>
        {
            await SynchronizeAsync(ct);
            StatusMessage = $"Download folder set to '{DownloadRootPath}'.";
        });
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
    var podcasts = await _podcastManager.GetPodcastsAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var podcast in podcasts)
        {
            var viewModel = UpsertPodcastViewModel(podcast);
            seen.Add(viewModel.Id);
        }

        for (var i = Podcasts.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Podcasts[i].Id))
            {
                Podcasts.RemoveAt(i);
            }
        }

        SortPodcasts();
        SelectedPodcast ??= Podcasts.FirstOrDefault();
        SelectedEpisode ??= SelectedPodcast?.Episodes.FirstOrDefault();
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        IsBusy = true;
        UpdateCommandStates();

        try
        {
            await action(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            UpdateCommandStates();
        }
    }

    private PodcastItemViewModel UpsertPodcastViewModel(Podcast podcast)
    {
        var existing = Podcasts.FirstOrDefault(p => string.Equals(p.Id, podcast.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var vm = new PodcastItemViewModel(podcast, _imageCache);
            Podcasts.Add(vm);
            SortPodcasts();
            return vm;
        }

        existing.UpdateFrom(podcast);
        SortPodcasts();
        return existing;
    }

    private void SortPodcasts()
    {
        var ordered = Podcasts.OrderByDescending(p => p.LastUpdated).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var index = Podcasts.IndexOf(current);
            if (index != i)
            {
                Podcasts.Move(index, i);
            }
        }
    }

    partial void OnFeedUrlChanged(string? value)
    {
        SubscribeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPodcastChanged(PodcastItemViewModel? value)
    {
        SelectedEpisode = value?.Episodes.FirstOrDefault();
        UpdateCommandStates();
    }

    partial void OnSelectedEpisodeChanged(EpisodeItemViewModel? value)
    {
        UpdateCommandStates();
    }

    partial void OnIsBusyChanged(bool value)
    {
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        SubscribeCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        DownloadAllCommand.NotifyCanExecuteChanged();
        SubscribeToSearchResultCommand.NotifyCanExecuteChanged();
    RefreshAllPodcastsCommand.NotifyCanExecuteChanged();
        ClearSearchResultsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTermChanged(string? value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        ClearSearchResultsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSearchResultChanged(PodcastSearchResultViewModel? value)
    {
        SubscribeToSearchResultCommand.NotifyCanExecuteChanged();
    }
}
