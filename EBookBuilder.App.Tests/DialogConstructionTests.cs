using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.App.Views;
using Lpubsppop01.EBookBuilder.Core;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;

// Importing the whole Shapes namespace would collide with System.IO.Path, so only the types used are aliased in.
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>
/// Confirmation that the screens can be constructed and operated.
/// </summary>
/// <remarks>
/// <para>
/// The fields corresponding to XAML <c>x:Name</c> are assigned by the
/// <c>InitializeComponent</c> the XAML compiler generates. Defining them yourself
/// makes overload resolution prefer your definition, leaving the field null.
/// The screen then crashes with <c>NullReferenceException</c> the moment it is opened.
/// </para>
/// <para>
/// It still compiles, so there is no way to check except by actually constructing the screen.
/// </para>
/// </remarks>
public class DialogConstructionTests
{
    #region Progress dialog

    [AvaloniaFact]
    public void ProgressDialogCanBeConstructed()
    {
        var dialog = new ProgressDialog("Working...", null);

        var message = dialog.FindControl<TextBlock>("ctrlMessage");
        Assert.NotNull(message);
        Assert.Equal("Working...", message.Text);
        Assert.NotNull(dialog.FindControl<ProgressBar>("ctrlProgress"));
    }

    [AvaloniaFact]
    public async Task ProgressDialogDoesNotCrashWhenItReceivesProgress()
    {
        // Progress is posted from a background thread to the UI thread, where it touches the controls.
        // If the controls are null, it crashes on this path (the cause of the crash during rotation).
        var dialog = new ProgressDialog("Working...", (progress, _) =>
        {
            for (var i = 1; i <= 3; ++i) progress.Report(new PageProgress(i, 3));
            return Task.CompletedTask;
        });

        dialog.Show();

        var message = dialog.FindControl<TextBlock>("ctrlMessage")!;
        var bar = dialog.FindControl<ProgressBar>("ctrlProgress")!;

        for (var i = 0; i < 200 && message.Text != "3 / 3 (100%)"; ++i)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.Equal("3 / 3 (100%)", message.Text);
        Assert.Equal(100, bar.Value);
    }

    #endregion

    #region Message dialog

    [AvaloniaFact]
    public void MessageDialogCanBeConstructed()
    {
        var dialog = new MessageDialog("Title", "Body text", showsCancel: true);

        var message = dialog.FindControl<TextBlock>("ctrlMessage");
        Assert.NotNull(message);
        Assert.Equal("Body text", message.Text);

        var cancel = dialog.FindControl<Button>("ctrlCancel");
        Assert.NotNull(cancel);
        Assert.True(cancel.IsVisible);
        Assert.NotNull(dialog.FindControl<Button>("ctrlOk"));
    }

    [AvaloniaFact]
    public void MessageDialogHidesTheCancelButtonForNotifications()
    {
        var dialog = new MessageDialog("Title", "Body text", showsCancel: false);

        Assert.False(dialog.FindControl<Button>("ctrlCancel")!.IsVisible);
    }

    #endregion

    #region Crop dialog

    [AvaloniaFact]
    public void CropDialogCanBeConstructed()
    {
        using var pages = TestPages.Create(1, width: 60, height: 90);
        var settings = new CropSettings { SourceSize = new ImageSize(60, 90) };

        var dialog = new CropDialog(settings, Path.Combine(pages.Path, "0.jpg"));

        // Showing it runs the red frame calculation (which crashes here if a control is null)
        dialog.Show();
        dialog.Close();
    }

    [AvaloniaFact]
    public void CropDialogRedFramesFollowTheMargins()
    {
        using var pages = TestPages.Create(1, width: 60, height: 90);
        var settings = new CropSettings { SourceSize = new ImageSize(60, 90) };
        var dialog = new CropDialog(settings, Path.Combine(pages.Path, "0.jpg"));
        dialog.Show();

        var maskTop = dialog.FindControl<Rectangle>("ctrlMaskTop")!;
        var maskLeft = dialog.FindControl<Rectangle>("ctrlMaskLeft")!;

        // Right after showing there is nothing to remove, so the masks are not shown
        Assert.Equal(0, maskTop.Height);
        Assert.Equal(0, maskLeft.Width);

        settings.Top = 10;
        settings.Left = 5;

        Assert.True(maskTop.Height > 0, "the top mask is not shown");
        Assert.True(maskLeft.Width > 0, "the left mask is not shown");

        dialog.Close();
    }

    [AvaloniaFact]
    public void CropDialogSaysHowManyPagesTheMarginsAreAppliedTo()
    {
        using var pages = TestPages.Create(1, width: 60, height: 90);
        var settings = new CropSettings { SourceSize = new ImageSize(60, 90) };
        var dialog = new CropDialog(settings, Path.Combine(pages.Path, "0.jpg"));
        dialog.Show();

        // A single page is the ordinary case, so nothing is said about it
        var notice = dialog.FindControl<TextBlock>("ctrlSeveralPagesNotice")!;
        Assert.False(notice.IsVisible);

        settings.TargetCount = 3;

        Assert.True(notice.IsVisible);
        Assert.Contains("applied to 3 pages", notice.Text);

        // The sizes are not always exactly equal, so how far they differ is reported as well
        settings.SizeDifference = 3;

        Assert.Contains("up to 3 pixels", notice.Text);

        dialog.Close();
    }

    [AvaloniaFact]
    public void CropDialogMasksCoverAllFourCorners()
    {
        using var pages = TestPages.Create(1, width: 60, height: 90);
        var settings = new CropSettings { SourceSize = new ImageSize(60, 90) };
        var dialog = new CropDialog(settings, Path.Combine(pages.Path, "0.jpg"));
        dialog.Show();

        settings.Top = 10;
        settings.Bottom = 20;
        settings.Left = 5;
        settings.Right = 5;

        // The mask heights for top and bottom correspond to the margin ratio (top to bottom 1:2)
        var maskTop = dialog.FindControl<Rectangle>("ctrlMaskTop")!;
        var maskBottom = dialog.FindControl<Rectangle>("ctrlMaskBottom")!;
        Assert.True(maskTop.Height > 0);
        Assert.True(maskBottom.Height > maskTop.Height, "the bottom margin should be larger");

        dialog.Close();
    }

    #endregion

    #region Build dialog and main window

    [AvaloniaFact]
    public void BuildDialogCanBeConstructed()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/out.cbz" };
        var dialog = new BuildDialog(settings, new FakeDialogService(), "/tmp");

        Assert.Equal(settings, dialog.DataContext);
    }

    /// <summary>
    /// PDF stores JPEG page images and has no PNG ones, and the page resolution only means
    /// something for a PDF, so each control is offered only where it applies.
    /// </summary>
    [AvaloniaFact]
    public void BuildDialogOffersEachSettingOnlyWhereItApplies()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/out.cbz" };
        var dialog = new BuildDialog(settings, new FakeDialogService(), "/tmp");
        dialog.Show();

        var imageFormatRow = dialog.FindControl<StackPanel>("ctrlImageFormatRow")!;
        // IsEnabled is what the binding sets on the row; IsEffectivelyEnabled is what the control
        // inside it ends up with, and so what decides whether the user can actually type in it.
        var pageDpi = dialog.FindControl<NumericUpDown>("ctrlPageDpi")!;

        Assert.True(imageFormatRow.IsEnabled);
        Assert.False(pageDpi.IsEffectivelyEnabled);

        settings.ContainerKind = BuildContainerKind.Pdf;

        Assert.False(imageFormatRow.IsEnabled);
        Assert.True(pageDpi.IsEffectivelyEnabled);

        dialog.Close();
    }

    /// <summary>
    /// The window sizes itself to its contents, so a row added to the grid has to be measured
    /// rather than assumed: a taller grid would otherwise run off the bottom of the window.
    /// </summary>
    [AvaloniaFact]
    public void BuildDialogLaysOutEveryRowInsideTheWindow()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/out.cbz" };
        var dialog = new BuildDialog(settings, new FakeDialogService(), "/tmp");
        dialog.Show();

        var pageDpi = dialog.FindControl<NumericUpDown>("ctrlPageDpi")!;
        var bottom = pageDpi.TranslatePoint(new Point(0, pageDpi.Bounds.Height), dialog)!.Value.Y;

        Assert.True(pageDpi.Bounds.Height > 0, "the page resolution input was not laid out");
        Assert.True(pageDpi.Bounds.Width > 0, "the page resolution input has no width");
        Assert.True(
            bottom <= dialog.ClientSize.Height,
            $"the page resolution row ends at {bottom} in a window {dialog.ClientSize.Height} tall");

        dialog.Close();
    }

    [AvaloniaFact]
    public void MainWindowCanBeConstructed()
    {
        using var pages = TestPages.Create(3);
        var viewModel = new MainViewModel(
            new FakeFolderPicker(), new FakeDialogService(), new FakeShellDialogs(), new AppSettings());
        viewModel.OpenDirectory(pages.Path);

        var window = new MainWindow(viewModel, new AppSettings());

        Assert.Equal(viewModel, window.DataContext);
    }

    #endregion
}
