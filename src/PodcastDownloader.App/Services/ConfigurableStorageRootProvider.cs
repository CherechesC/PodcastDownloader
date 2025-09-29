using System;
using System.IO;
using System.Text.Json;
using PodcastDownloader.Core.Interfaces;

namespace PodcastDownloader.App.Services;

public class ConfigurableStorageRootProvider : IStorageRootProvider
{
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;
    private string _rootPath;

    public ConfigurableStorageRootProvider()
    {
        _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodcastDownloader");
        _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
        _rootPath = LoadRootPath() ?? GetDefaultRootPath();
    }

    public string GetRootPath()
    {
        EnsureDirectoryExists(_rootPath);
        return _rootPath;
    }

    public void SetRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path cannot be null or whitespace.", nameof(rootPath));
        }

        if (!Path.IsPathRooted(rootPath))
        {
            throw new ArgumentException("Root path must be absolute.", nameof(rootPath));
        }

        _rootPath = rootPath;
        EnsureDirectoryExists(_rootPath);
        SaveSettings();
    }

    private string? LoadRootPath()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<StorageSettings>(json);
            return settings?.RootPath;
        }
        catch
        {
            return null;
        }
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(_settingsDirectory);
        var settings = new StorageSettings { RootPath = _rootPath };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    private static string GetDefaultRootPath()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(baseDirectory, "PodcastDownloader");
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private sealed class StorageSettings
    {
        public string RootPath { get; set; } = string.Empty;
    }
}
