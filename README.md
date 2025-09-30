# Podcast Downloader

A cross-platform desktop application built with [Avalonia UI](https://avaloniaui.net/) and .NET 8 for discovering podcasts, subscribing to feeds, browsing episodes, and downloading them for offline listening.

## Project layout

```
PodcastDownloader/
├─ src/
│  ├─ PodcastDownloader.App/        # Avalonia UI desktop front-end (MVVM)
│  └─ PodcastDownloader.Core/       # Core domain models and services
├─ tests/
│  └─ PodcastDownloader.Tests/      # xUnit automated tests
└─ README.md
```

## Getting started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Restore & build

```powershell
cd PodcastDownloader
dotnet build PodcastDownloader.sln
```

### Run the desktop app

```powershell
cd PodcastDownloader
dotnet run --project src/PodcastDownloader.App
```

### Run tests

```powershell
cd PodcastDownloader
dotnet test
```

## Key features

- **Podcast discovery** powered by Apple's public Search API, letting you explore new shows and subscribe directly from the app.
- **MVVM-first UI** with Avalonia and CommunityToolkit MVVM, including per-podcast download indicators, episode progress bars, and a global "Refresh All" action.
- **Persistent storage** via a JSON-backed repository that stores relative paths beneath a user-configurable download root for portability.
- **Media management** that caches podcast artwork, organizes episodes and artwork in per-podcast folders, and supports single or bulk episode downloads with progress feedback.
- **Core domain services** for feed parsing, download orchestration, and storage abstraction, all wired with dependency injection and typed `HttpClient` instances.
- **Unit tests** covering repository persistence, discovery integration, and download workflows.

## Next steps

- Offer additional discovery providers (e.g., Podcast Index) and richer filtering options.
- Add scheduled background refreshes and notifications when new episodes arrive.
- Introduce smarter download queue management with pause/resume and bandwidth controls.
- Expose import/export for OPML or backup/restore of subscriptions.
