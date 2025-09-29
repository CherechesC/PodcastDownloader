using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PodcastDownloader.App.ViewModels;
using PodcastDownloader.App.ViewModels.Design;

namespace PodcastDownloader.App.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow() : this(ResolveViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await _viewModel.InitializeAsync();
    }

    private static MainWindowViewModel ResolveViewModel()
    {
        if (!Design.IsDesignMode && App.Services is IServiceProvider provider)
        {
            var resolved = provider.GetService<MainWindowViewModel>();
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return MainWindowViewModelFactory.CreateDesignInstance();
    }
}