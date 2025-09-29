namespace PodcastDownloader.Core.Interfaces;

public interface IStorageRootProvider
{
    string GetRootPath();

    void SetRootPath(string rootPath);
}
