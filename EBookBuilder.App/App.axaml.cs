using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lpubsppop01.EBookBuilder.App.Services;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.App.Views;

namespace Lpubsppop01.EBookBuilder.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow(desktop.Args);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Assembles the dependencies and creates the main screen.</summary>
    /// <remarks>
    /// There are only a few parts, so they are wired together directly instead of bringing in a DI container.
    /// The window is created after the view model, so it is passed in through <see cref="WindowOwner"/>.
    /// </remarks>
    static Window CreateMainWindow(string[]? args)
    {
        var settings = AppSettings.Load();
        var owner = new WindowOwner();
        var dialogs = new DialogService(() => owner.Value);
        var folderPicker = new StorageProviderFolderPicker(() => owner.Value);

        MainViewModel? viewModel = null;
        var shell = new ShellDialogs(() => owner.Value, dialogs, () => viewModel?.TargetDirectoryPath);

        viewModel = new MainViewModel(folderPicker, dialogs, shell, settings);

        var window = new MainWindow(viewModel, settings);
        owner.Value = window;

        // If a folder was passed as a startup argument, open it.
        // As in the original WPF version, this makes it possible to launch the app with an associated folder.
        if (args is { Length: > 0 } && Directory.Exists(args[0]))
        {
            viewModel.OpenDirectory(args[0]);
        }

        return window;
    }
}
