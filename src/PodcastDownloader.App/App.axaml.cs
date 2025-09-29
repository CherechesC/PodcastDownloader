using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PodcastDownloader.App.Services;
using PodcastDownloader.App.ViewModels;
using PodcastDownloader.App.Views;
using PodcastDownloader.Core.Interfaces;
using PodcastDownloader.Core.Services;
using PodcastDownloader.Core.Storage;

namespace PodcastDownloader.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            _serviceProvider = serviceProvider;
            Services = serviceProvider;

            desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) =>
            {
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                Services = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

    services.AddSingleton<IStorageRootProvider, ConfigurableStorageRootProvider>();
    services.AddSingleton<IFolderPickerService, FolderPickerService>();

    services.AddSingleton<IPodcastRepository, JsonFilePodcastRepository>();
    services.AddSingleton<IPodcastStorageService, FileSystemStorageService>();

        services.AddHttpClient<IPodcastFeedService, PodcastFeedService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PodcastDownloader/1.0 (+https://github.com/example/podcastdownloader)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IPodcastDownloadService, PodcastDownloadService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddHttpClient<IPodcastDiscoveryService, ApplePodcastDiscoveryService>(client =>
        {
            client.BaseAddress = new Uri("https://itunes.apple.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PodcastDownloader/1.0 (+https://github.com/example/podcastdownloader)");
        });

        services.AddHttpClient("podcast-artwork", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PodcastDownloader/1.0 (+https://github.com/example/podcastdownloader)");
        });

        services.AddSingleton<IImageCache, ImageCache>();

        services.AddSingleton<PodcastManager>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}