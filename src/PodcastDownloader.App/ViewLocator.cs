using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
using PodcastDownloader.App.ViewModels;

namespace PodcastDownloader.App;

public class ViewLocator : IDataTemplate
{

    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            if (App.Services is IServiceProvider provider)
            {
                try
                {
                    return (Control)ActivatorUtilities.CreateInstance(provider, type);
                }
                catch (InvalidOperationException)
                {
                    // Fall back to parameterless constructor below.
                }
            }

            return type.GetConstructor(Type.EmptyTypes) is not null
                ? (Control)Activator.CreateInstance(type)!
                : new TextBlock { Text = $"Unable to create view: {name}" };
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
