using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Lpubsppop01.EBookBuilder.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>
/// Configuration for running Avalonia without showing a screen.
/// </summary>
/// <remarks>
/// <para>
/// Uses a bare <see cref="Application"/> instead of <c>App</c>.
/// <c>App</c> creates the main window on startup, which gets in the way in tests.
/// </para>
/// <para>
/// The theme and font, however, are loaded exactly as in the real application.
/// Button margins and text width affect the layout, so omitting them would give
/// results that differ from the real screen and the layout tests would be meaningless.
/// </para>
/// </remarks>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .AfterSetup(_ => Application.Current!.Styles.Add(new FluentTheme()));
}
