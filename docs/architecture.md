# Architecture overview

The solution follows a layered structure that separates UI concerns from core business logic and infrastructure.

## Layers

| Layer | Project | Purpose |
|-------|---------|---------|
| Presentation | `PodcastDownloader.App` | Avalonia UI desktop client implemented with MVVM and dependency injection. |
| Domain & Services | `PodcastDownloader.Core` | Podcast domain models, RSS feed parsing, download coordination, and abstraction interfaces. |
| Tests | `PodcastDownloader.Tests` | Automated tests that exercise the core services. |

## Core services

- `IPodcastFeedService` → `PodcastFeedService`
  - Fetches RSS/Atom feeds via `HttpClient`.
  - Uses `System.ServiceModel.Syndication` to parse feed metadata and episodes.
- `IPodcastDownloadService` → `PodcastDownloadService`
  - Streams episode media to disk while reporting progress.
  - Relies on `IPodcastStorageService` to determine file destinations.
- `IPodcastRepository` → `InMemoryPodcastRepository`
  - Thread-safe in-memory storage for subscribed podcasts and download state.
  - Designed to be replaced by a persistent repository in the future.
- `PodcastManager`
  - High-level orchestrator for subscribe/refresh/download/remove operations.
  - Merges downloaded episode metadata when refreshing feeds.

## UI composition

- `MainWindow` hosts the main layout with sections for subscriptions, episodes, and actions.
- `MainWindowViewModel` exposes observable state and async commands.
- `PodcastItemViewModel` and `EpisodeItemViewModel` adapt domain models for the UI, tracking download progress and formatted metadata.
- Dependency injection wires view models, services, and views together.

## Extensibility points

- Swap `IPodcastRepository` with a disk-backed implementation (JSON, LiteDB, SQLite, etc.).
- Introduce caching or throttling policies by decorating `IPodcastFeedService`.
- Replace `IPodcastDownloadService` to add retry logic, parallel downloads, or resumable transfers.
- Extend the UI with additional views (e.g., settings, search) leveraging the existing MVVM wiring and view locator.
