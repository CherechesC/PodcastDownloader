# Podcast Downloader

A cross-platform desktop application built with [Avalonia UI](https://avaloniaui.net/) and .NET 8 for subscribing to podcast feeds, browsing episodes, and downloading them for offline listening.

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

- **MVVM-first UI** with Avalonia, leveraging CommunityToolkit MVVM for observable state and async commands.
- **Core domain layer** exposing podcast/episode models, feed parsing, download management, and storage services.
- **Dependency injection** via `Microsoft.Extensions.DependencyInjection` with typed `HttpClient` instances for feed parsing and media downloads.
- **Pluggable persistence** through `IPodcastRepository`, with an in-memory default and a filesystem storage service for downloaded media.
- **Unit tests** covering podcast subscription and download flows.

## Next steps

- Persist subscriptions to disk (e.g., JSON or LiteDB repository).
- Enhance the UI with artwork previews, search, and filtering.
- Queue and track multiple downloads concurrently with cancellation controls.
- Integrate notifications when new episodes are available.
