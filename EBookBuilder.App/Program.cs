using Avalonia;

namespace Lpubsppop01.EBookBuilder.App;

internal static class Program
{
    // Avalonia and the other libraries must not be touched before Avalonia is initialized.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // This is also used by the visual designer, so do not remove it.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
