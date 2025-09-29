using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace PodcastDownloader.App.Services;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select Podcast Download Folder"
        };

        var results = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(options);
        var folder = results.FirstOrDefault();
        if (folder is null)
        {
            return null;
        }

        if (folder.TryGetLocalPath() is { } localPath)
        {
            return localPath;
        }

        return folder.Path.LocalPath;
    }
}
