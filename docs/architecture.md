# Architecture overview

The solution follows a layered structure that separates UI concerns from core business logic and infrastructure.

## Layers

| Layer | Project | Purpose |
|-------|---------|---------|
| Presentation | `PodcastDownloader.App` | Avalonia UI desktop client implemented with MVVM, discovery UI, and download controls. |
| Domain & Services | `PodcastDownloader.Core` | Podcast domain models, RSS feed parsing, discovery, download coordination, and storage abstractions. |
| Infrastructure | `PodcastDownloader.Core` | JSON-backed repository, filesystem storage helpers, image caching, and discovery integrations. |
| Tests | `PodcastDownloader.Tests` | Automated tests validating persistence, discovery, and manager workflows. |

## Core services

- `IPodcastFeedService` → `PodcastFeedService`
  - Fetches RSS/Atom feeds via `HttpClient`.
  - Uses `System.ServiceModel.Syndication` to parse feed metadata and episodes.
- `IPodcastDownloadService` → `PodcastDownloadService`
  - Streams episode media to disk while reporting progress.
  - Relies on `IPodcastStorageService` to determine file destinations.
- `IPodcastRepository` → `JsonFilePodcastRepository`
  - Persists podcasts and episodes to `podcasts.json`, storing relative paths so the library remains portable.
  - Restores download metadata (local files, artwork) when the app restarts or refreshes feeds.
- `PodcastManager`
  - High-level orchestrator for subscribe/refresh/download/remove operations.
  - Merges downloaded episode metadata when refreshing feeds.
  - Ensures artwork is cached locally and re-used for subsequent sessions.
- `IPodcastDiscoveryService` → `ApplePodcastDiscoveryService`
  - Calls Apple's open podcast search API to return feed URLs, titles, authors, and artwork for in-app discovery.
- `IPodcastStorageService` → `FileSystemStorageService`
  - Creates a per-podcast folder hierarchy (including an `Art` subfolder) beneath the chosen storage root.
  - Exposes helpers for episode media paths and artwork caching.
- `IImageCache` → `ImageCache`
  - Downloads and reuses artwork bitmaps for the UI using a dedicated `HttpClient`.

## UI composition

- `MainWindow` hosts the main layout with sections for subscriptions, episodes, discovery search results, and status messages.
- `MainWindowViewModel` exposes observable state and async commands, including search, subscribe, refresh-all, and bulk download actions.
- `PodcastItemViewModel` and `EpisodeItemViewModel` adapt domain models for the UI, tracking download progress and formatted metadata.
- `PodcastItemViewModel` aggregates episode download state, surfaces relative "last updated" timestamps, and highlights podcasts that need refreshing.
- Dependency injection wires view models, services, and views together.

## Extensibility points

- Swap or extend discovery providers by implementing `IPodcastDiscoveryService` (e.g., Podcast Index, Listen Notes).
- Replace `IPodcastRepository` with an alternative persistence strategy (database, cloud sync) while reusing the storage abstractions.
- Decorate `IPodcastFeedService` or `IPodcastDownloadService` to add caching, retry, or throttling policies.
- Extend the UI with additional views (settings, notifications, smart playlists) leveraging the existing MVVM wiring and view locator.
