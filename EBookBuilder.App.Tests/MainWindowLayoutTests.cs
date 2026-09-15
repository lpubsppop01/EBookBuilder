using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.App.Views;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>
/// Verification of the main screen layout.
/// </summary>
/// <remarks>
/// If the button row uses a non-wrapping layout, it overflows its column when the width
/// is insufficient and overlaps the adjacent preview area. Text width varies with the font,
/// so the layout is actually built and the coordinates measured.
/// </remarks>
public class MainWindowLayoutTests
{
    static MainWindow ShowWindow(TestPages pages, double width, double height)
    {
        var viewModel = new MainViewModel(
            new FakeFolderPicker(), new FakeDialogService(), new FakeShellDialogs(), new AppSettings());
        viewModel.OpenDirectory(pages.Path);

        // The window size comes from the settings, so put the desired values there.
        var settings = new AppSettings { WindowWidth = width, WindowHeight = height };
        var window = new MainWindow(viewModel, settings);
        window.Show();

        return window;
    }

    /// <summary>Confirms the window is laid out at the specified size (that the premise for measuring still holds).</summary>
    static void AssertClientSize(Window window, double width, double height) =>
        Assert.Equal(new Size(width, height), window.ClientSize);

    /// <summary>The position of the element's left edge within the window.</summary>
    static double LeftEdge(Control control, Visual root) =>
        control.TranslatePoint(new Point(0, 0), root)?.X ?? double.NaN;

    /// <summary>The position of the element's right edge within the window.</summary>
    static double RightEdge(Control control, Visual root) =>
        control.TranslatePoint(new Point(control.Bounds.Width, 0), root)?.X ?? double.NaN;

    /// <summary>The number of rows in the layout. Elements in the same row share a vertical position.</summary>
    /// <remarks>Counting by horizontal position would count elements in the same row as separate ones.</remarks>
    static int CountRows(Panel panel, Visual root) =>
        panel.Children.OfType<Control>()
            .Select(control => Math.Round(control.TranslatePoint(new Point(0, 0), root)?.Y ?? double.NaN))
            .Distinct()
            .Count();

    [AvaloniaTheory]
    [InlineData(900, 480)]   // default size
    [InlineData(700, 480)]
    [InlineData(600, 400)]   // minimum size
    public void ButtonsDoNotOverflowIntoThePreviewArea(double width, double height)
    {
        using var pages = TestPages.Create(3);
        var window = ShowWindow(pages, width, height);
        AssertClientSize(window, width, height);

        var preview = window.FindControl<Border>("ctrlPreviewArea")!;
        var previewLeft = LeftEdge(preview, window);

        // Unless layout has finished, the following comparisons are meaningless
        Assert.True(previewLeft > 0, $"the position of the preview area could not be obtained ({previewLeft})");

        foreach (var name in new[] { "ctrlSingleButtons", "ctrlWholeButtons" })
        {
            var panel = window.FindControl<Panel>(name)!;
            Assert.NotEmpty(panel.Children);

            foreach (var button in panel.Children.OfType<Button>())
            {
                var right = RightEdge(button, window);
                Assert.True(
                    right <= previewLeft + 0.5,
                    $"{width}x{height}: \"{button.Content}\" overflows (right edge {right:F1} > preview left edge {previewLeft:F1})");
            }
        }

        window.Close();
    }

    [AvaloniaFact]
    public void ButtonsWrapWhenTheyWouldOverflow()
    {
        // With a non-wrapping layout, the Single row reaches the preview area at this width.
        using var pages = TestPages.Create(3);
        var window = ShowWindow(pages, 700, 480);
        AssertClientSize(window, 700, 480);

        var panel = window.FindControl<Panel>("ctrlSingleButtons")!;

        Assert.True(CountRows(panel, window) > 1, "not wrapped");

        window.Close();
    }

    [AvaloniaFact]
    public void ButtonsDoNotWrapWhenTheWidthIsSufficient()
    {
        using var pages = TestPages.Create(3);
        var window = ShowWindow(pages, 1400, 480);
        AssertClientSize(window, 1400, 480);

        var panel = window.FindControl<Panel>("ctrlWholeButtons")!;

        Assert.Equal(1, CountRows(panel, window));

        window.Close();
    }
}
