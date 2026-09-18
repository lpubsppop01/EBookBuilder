using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>Verification of main screen operations.</summary>
/// <remarks>
/// This layer is where the app crashed when everything was checked. The same path is exercised for real.
/// </remarks>
public class MainViewModelTests
{
    static MainViewModel CreateViewModel(
        FakeDialogService dialogs,
        FakeShellDialogs? shell = null,
        FakeFolderPicker? picker = null) =>
        new(picker ?? new FakeFolderPicker(), dialogs, shell ?? new FakeShellDialogs(), new AppSettings());

    #region Loading pages

    [AvaloniaFact]
    public void OpeningAFolderListsThePages()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());

        viewModel.OpenDirectory(pages.Path);

        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], viewModel.Pages.Select(page => page.Filename));
        Assert.Equal("0.jpg", viewModel.SelectedPage?.Filename);
    }

    [AvaloniaFact]
    public void PagesAreInitiallyUnchecked()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());

        viewModel.OpenDirectory(pages.Path);

        Assert.All(viewModel.Pages, page => Assert.False(page.IsChecked));
    }

    #endregion

    #region Check operations

    [AvaloniaFact]
    public void CheckingAllDoesNotRaiseAnError()
    {
        // This is the reported bug itself.
        // The operation target was found with SingleOrDefault, so the moment two or more
        // pages were checked it threw InvalidOperationException and the app crashed.
        using var pages = TestPages.Create(5);
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);

        viewModel.CheckAllCommand.Execute(null);

        Assert.All(viewModel.Pages, page => Assert.True(page.IsChecked));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void CheckingOddPagesDoesNotRaiseAnError()
    {
        using var pages = TestPages.Create(6);
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);

        viewModel.CheckOddCommand.Execute(null);

        Assert.Equal([true, false, true, false, true, false], viewModel.Pages.Select(page => page.IsChecked));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void CheckingEvenPagesDoesNotRaiseAnError()
    {
        using var pages = TestPages.Create(6);
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);

        viewModel.CheckEvenCommand.Execute(null);

        Assert.Equal([false, true, false, true, false, true], viewModel.Pages.Select(page => page.IsChecked));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void CheckingAllDoesNotCrashEvenWithManyPages()
    {
        using var pages = TestPages.Create(200);
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);

        viewModel.CheckAllCommand.Execute(null);
        viewModel.UncheckAllCommand.Execute(null);
        viewModel.CheckAllCommand.Execute(null);

        Assert.All(viewModel.Pages, page => Assert.True(page.IsChecked));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void AllChecksCanBeCleared()
    {
        using var pages = TestPages.Create(4);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        viewModel.CheckAllCommand.Execute(null);
        viewModel.UncheckAllCommand.Execute(null);

        Assert.All(viewModel.Pages, page => Assert.False(page.IsChecked));
    }

    [AvaloniaFact]
    public void ChecksAboveTheSelectionCanBeCleared()
    {
        using var pages = TestPages.Create(5);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);
        viewModel.SelectedPage = viewModel.Pages[3];

        viewModel.UncheckUpperCommand.Execute(null);

        Assert.Equal([false, false, false, true, true], viewModel.Pages.Select(page => page.IsChecked));
    }

    [AvaloniaFact]
    public void ChecksBelowTheSelectionCanBeCleared()
    {
        using var pages = TestPages.Create(5);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);
        viewModel.SelectedPage = viewModel.Pages[1];

        viewModel.UncheckLowerCommand.Execute(null);

        Assert.Equal([true, true, false, false, false], viewModel.Pages.Select(page => page.IsChecked));
    }

    #endregion

    #region Enabling and disabling operations

    [AvaloniaFact]
    public void SinglePageOperationsAreDisabledWithoutAnyCheck()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        // Build is not a single page operation, so it is out of scope here (covered separately in the build section)
        Assert.False(viewModel.CropCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.False(viewModel.DuplicateToNextCommand.CanExecute(null));
        Assert.False(viewModel.MoveToLastCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CheckingExactlyOneEnablesEveryOperation()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        // The same path as clicking the checkbox in the list directly.
        // It does not go through a command, so unless this updates the state the buttons stay unclickable.
        viewModel.Pages[1].IsChecked = true;

        Assert.True(viewModel.CropCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.BuildCommand.CanExecute(null));
        Assert.True(viewModel.DuplicateToNextCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void UncheckingDisablesSinglePageOperationsAgain()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        viewModel.Pages[1].IsChecked = true;
        Assert.True(viewModel.CropCommand.CanExecute(null));

        viewModel.Pages[1].IsChecked = false;

        Assert.False(viewModel.CropCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void CheckingASecondPageDisablesOperationsThatNeedASingleTarget()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        viewModel.Pages[0].IsChecked = true;
        Assert.True(viewModel.DuplicateToNextCommand.CanExecute(null));

        // It crashed here
        viewModel.Pages[1].IsChecked = true;

        Assert.False(viewModel.DuplicateToNextCommand.CanExecute(null));
        Assert.False(viewModel.MoveToLastCommand.CanExecute(null));

        // Delete and crop take every checked page as their target, so they stay usable
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.CropCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void OperationsThatNeedASingleTargetAreDisabledWithTwoOrMoreChecks()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        // Check everything, then re-query whether the commands can execute
        viewModel.CheckAllCommand.Execute(null);

        Assert.False(viewModel.DuplicateToNextCommand.CanExecute(null));
        Assert.False(viewModel.MoveToLastCommand.CanExecute(null));

        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.CropCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void SinglePageOperationsAreDisabledForNonSerialNames()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        // Check exactly one, and make only its name non-serial
        viewModel.Pages[0].Filename = "zzz.jpg";
        viewModel.Pages[0].IsChecked = true;

        Assert.False(viewModel.CropCommand.CanExecute(null));
    }

    #endregion

    #region Build

    [AvaloniaFact]
    public void BuildIsPossibleWithoutAnyCheck()
    {
        // The build targets the whole folder, so checks are irrelevant.
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        Assert.True(viewModel.BuildCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void BuildIsPossibleNoMatterHowManyPagesAreChecked()
    {
        // It previously had a single-selection condition attached,
        // so checking two or more pages disabled the build button.
        using var pages = TestPages.Create(4);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        Assert.True(viewModel.BuildCommand.CanExecute(null));

        viewModel.Pages[0].IsChecked = true;
        Assert.True(viewModel.BuildCommand.CanExecute(null));

        viewModel.Pages[1].IsChecked = true;
        Assert.True(viewModel.BuildCommand.CanExecute(null));

        viewModel.CheckAllCommand.Execute(null);
        Assert.True(viewModel.BuildCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void BuildIsImpossibleWithoutPages()
    {
        using var pages = TestPages.Create(0);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        Assert.False(viewModel.BuildCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task BuildingProducesACbz()
    {
        using var pages = TestPages.Create(3);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs { BuildResult = true };

        // The settings file also goes in the temporary folder, so the real settings are not overwritten.
        var settings = new AppSettings(Path.Combine(pages.Path, "settings.json"));
        var viewModel = new MainViewModel(new FakeFolderPicker(), dialogs, shell, settings);
        viewModel.OpenDirectory(pages.Path);

        var outputFilePath = pages.Path + ".cbz";
        try
        {
            viewModel.BuildCommand.Execute(null);

            for (var i = 0; i < 300 && !File.Exists(outputFilePath); ++i)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            // Even with nothing checked, all pages are packaged
            Assert.True(File.Exists(outputFilePath), "the CBZ was not created");
            Assert.NotNull(shell.LastBuildSettings);

            using var zip = System.IO.Compression.ZipFile.OpenRead(outputFilePath);
            Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], zip.Entries.Select(entry => entry.FullName));

            Assert.Contains(dialogs.Alerts, message => message.Contains("Created"));
        }
        finally
        {
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }

    /// <summary>
    /// The container chosen last time is remembered, and the output name follows it, so a PDF is
    /// not written under the name of a CBZ.
    /// </summary>
    [AvaloniaFact]
    public async Task BuildingProducesAPdfWhenPdfWasChosenLastTime()
    {
        using var pages = TestPages.Create(3);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs { BuildResult = true };

        var settings = new AppSettings(Path.Combine(pages.Path, "settings.json"))
        {
            ContainerKind = BuildContainerKind.Pdf,
        };
        var viewModel = new MainViewModel(new FakeFolderPicker(), dialogs, shell, settings);
        viewModel.OpenDirectory(pages.Path);

        var outputFilePath = pages.Path + ".pdf";
        try
        {
            viewModel.BuildCommand.Execute(null);

            for (var i = 0; i < 300 && !File.Exists(outputFilePath); ++i)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

            Assert.True(File.Exists(outputFilePath), "the PDF was not created");
            Assert.Equal(BuildContainerKind.Pdf, shell.LastBuildSettings!.ContainerKind);

            var bytes = File.ReadAllBytes(outputFilePath);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));

            Assert.Contains(dialogs.Alerts, message => message.Contains("Created"));
        }
        finally
        {
            if (File.Exists(outputFilePath)) File.Delete(outputFilePath);
        }
    }

    #endregion

    #region Deletion

    [AvaloniaFact]
    public void DeletionAsksForConfirmationFirst()
    {
        using var pages = TestPages.Create(3);
        var dialogs = new FakeDialogService { ConfirmResult = false };
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;

        viewModel.DeleteCommand.Execute(null);

        Assert.Single(dialogs.Confirms);
        Assert.Equal(3, viewModel.Pages.Count);
    }

    [AvaloniaFact]
    public void ConfirmingDeletionRemovesThePage()
    {
        using var pages = TestPages.Create(3);
        var dialogs = new FakeDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;

        viewModel.DeleteCommand.Execute(null);

        Assert.Equal(["0.jpg", "2.jpg"], viewModel.Pages.Select(page => page.Filename));
        Assert.False(File.Exists(Path.Combine(pages.Path, "1.jpg")));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void ConfirmingDeletionRemovesEveryCheckedPage()
    {
        using var pages = TestPages.Create(4);
        var dialogs = new FakeDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;
        viewModel.Pages[2].IsChecked = true;

        viewModel.DeleteCommand.Execute(null);

        Assert.Equal(["0.jpg", "3.jpg"], viewModel.Pages.Select(page => page.Filename));
        Assert.False(File.Exists(Path.Combine(pages.Path, "1.jpg")));
        Assert.False(File.Exists(Path.Combine(pages.Path, "2.jpg")));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void DeletingSeveralPagesAsksOnceAndNamesTheTargets()
    {
        using var pages = TestPages.Create(3);
        var dialogs = new FakeDialogService { ConfirmResult = false };
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[0].IsChecked = true;
        viewModel.Pages[2].IsChecked = true;

        viewModel.DeleteCommand.Execute(null);

        var message = Assert.Single(dialogs.Confirms);
        Assert.Contains("2 pages", message);
        Assert.Contains("0.jpg", message);
        Assert.Contains("2.jpg", message);
        Assert.Equal(3, viewModel.Pages.Count);
    }

    [AvaloniaFact]
    public void DeletingTheSelectedPageMovesTheSelection()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService { ConfirmResult = true });
        viewModel.OpenDirectory(pages.Path);
        Assert.Equal("0.jpg", viewModel.SelectedPage?.Filename);

        viewModel.Pages[0].IsChecked = true;
        viewModel.DeleteCommand.Execute(null);

        // The preview would have nothing to show if the selection stayed on the deleted page
        Assert.Equal("1.jpg", viewModel.SelectedPage?.Filename);
    }

    [AvaloniaFact]
    public void DeletingEveryPageLeavesNoSelection()
    {
        using var pages = TestPages.Create(2);
        var viewModel = CreateViewModel(new FakeDialogService { ConfirmResult = true });
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);

        viewModel.DeleteCommand.Execute(null);

        Assert.Empty(viewModel.Pages);
        Assert.Null(viewModel.SelectedPage);
    }

    #endregion

    #region Serial renaming

    [AvaloniaFact]
    public void SerialRenameUpdatesTheNamesInTheList()
    {
        using var pages = TestPages.Create(3);

        // Break the names on disk before opening
        File.Move(Path.Combine(pages.Path, "0.jpg"), Path.Combine(pages.Path, "c.jpg"));

        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dialogs);
        viewModel.OpenDirectory(pages.Path);
        Assert.Equal(["1.jpg", "2.jpg", "c.jpg"], viewModel.Pages.Select(page => page.Filename));

        viewModel.RenameCommand.Execute(null);

        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], viewModel.Pages.Select(page => page.Filename));
        Assert.Empty(dialogs.Alerts);
    }

    #endregion

    #region Cropping

    [AvaloniaFact]
    public void CropPassesTheSelectedPageSizeToTheDialog()
    {
        using var pages = TestPages.Create(3, width: 60, height: 90);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs { CropResult = false };  // pretend it was cancelled
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;

        viewModel.CropCommand.Execute(null);

        Assert.NotNull(shell.LastCropSettings);
        Assert.Equal(60, shell.LastCropSettings!.SourceSize.Width);
        Assert.Equal(90, shell.LastCropSettings.SourceSize.Height);
        Assert.Equal(1, shell.LastCropSettings.TargetCount);
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void CancellingTheCropChangesNothing()
    {
        using var pages = TestPages.Create(3, width: 60, height: 90);
        var shell = new FakeShellDialogs { CropResult = false };
        var viewModel = CreateViewModel(new FakeDialogService(), shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;

        viewModel.CropCommand.Execute(null);

        Assert.Equal(0, shell.RunCount);
        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], viewModel.Pages.Select(page => page.Filename));
    }

    [AvaloniaFact]
    public void CroppingSeveralPagesAppliesTheSameMarginsToAllOfThem()
    {
        using var pages = TestPages.Create(3, width: 60, height: 90);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs
        {
            CropResult = true,
            CropDialogAction = settings =>
            {
                settings.Left = 10;
                settings.Top = 5;
            },
        };
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.Pages[1].IsChecked = true;
        viewModel.Pages[2].IsChecked = true;

        viewModel.CropCommand.Execute(null);

        Assert.Equal(2, shell.LastCropSettings!.TargetCount);

        // Both checked pages end up 60-10 x 90-5, and the unchecked one is left alone
        foreach (var filename in new[] { "1.jpg", "2.jpg" })
        {
            Assert.Equal(
                new ImageSize(50, 85),
                PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, filename)));
        }
        Assert.Equal(
            new ImageSize(60, 90),
            PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "0.jpg")));
        Assert.Empty(dialogs.Alerts);
    }

    [AvaloniaFact]
    public void CroppingPagesOfDifferentSizesIsAborted()
    {
        using var pages = TestPages.Create(3, width: 60, height: 90);
        pages.WritePage("1.jpg", 100, 120);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs { CropResult = true };
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);

        viewModel.CropCommand.Execute(null);

        // The dialog is not even opened and nothing is rewritten
        Assert.Null(shell.LastCropSettings);
        Assert.Equal(0, shell.RunCount);

        // The reason is reported grouped by size, naming the page that stands out. The difference
        // from the reference is given in pixels and percent, so that the tolerance can be adjusted
        // against what was actually scanned.
        var message = Assert.Single(dialogs.Alerts);
        Assert.Contains("60 x 90 (reference): 2 pages (0.jpg, 2.jpg)", message);
        Assert.Contains("100 x 120 (40 pixels / 66.67% wider, 30 pixels / 33.33% taller): 1 page (1.jpg)", message);
        Assert.Contains("up to 4 pixels or 1.5%", message);
    }

    [AvaloniaFact]
    public void CroppingPagesThatDifferByAFewPixelsIsAccepted()
    {
        // The kind of difference a scan produces: the paper edge is detected a few pixels apart
        using var pages = TestPages.Create(3, width: 60, height: 90);
        pages.WritePage("1.jpg", 63, 90);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs
        {
            CropResult = true,
            CropDialogAction = settings =>
            {
                settings.Left = 10;
                settings.Top = 5;
            },
        };
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);

        viewModel.CropCommand.Execute(null);

        Assert.Empty(dialogs.Alerts);
        Assert.Equal(3, shell.LastCropSettings!.TargetCount);
        Assert.Equal(3, shell.LastCropSettings.SizeDifference);

        // The margins are the same on every page, so the wider page stays wider
        Assert.Equal(new ImageSize(50, 85), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "0.jpg")));
        Assert.Equal(new ImageSize(53, 85), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "1.jpg")));
    }

    [AvaloniaFact]
    public void CroppingPagesThatDifferByMoreThanTheMinimumIsAccepted()
    {
        // 10 pixels apart, which is 1% of the width: more than the fixed minimum, but within the
        // ratio. The pages of a real scan came out about this far apart.
        using var pages = TestPages.Create(2, width: 1000, height: 1400);
        pages.WritePage("1.jpg", 1010, 1400);
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs
        {
            CropResult = true,
            CropDialogAction = settings => settings.Left = 10,
        };
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);

        viewModel.CropCommand.Execute(null);

        Assert.Empty(dialogs.Alerts);
        Assert.Equal(10, shell.LastCropSettings!.SizeDifference);

        // The same margins are removed from both, so the wider page stays wider
        Assert.Equal(new ImageSize(990, 1400), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "0.jpg")));
        Assert.Equal(new ImageSize(1000, 1400), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "1.jpg")));
    }

    [AvaloniaFact]
    public void MarginsTooLargeForTheSmallestPageAreRejectedBeforeCropping()
    {
        // The margins fit the page the preview shows, but not the slightly smaller one beside it.
        // Without checking every target this would fail partway and crop only some of the pages.
        using var pages = TestPages.Create(2, width: 60, height: 90);
        pages.WritePage("0.jpg", 63, 90);   // the previewed page is the larger one
        var dialogs = new FakeDialogService();
        var shell = new FakeShellDialogs
        {
            CropResult = true,
            CropDialogAction = settings =>
            {
                settings.Left = 30;
                settings.Right = 30;
            },
        };
        var viewModel = CreateViewModel(dialogs, shell);
        viewModel.OpenDirectory(pages.Path);
        viewModel.CheckAllCommand.Execute(null);

        viewModel.CropCommand.Execute(null);

        Assert.Equal("The margins are too large.", Assert.Single(dialogs.Alerts));
        Assert.Equal(0, shell.RunCount);

        // Neither page was touched
        Assert.Equal(new ImageSize(63, 90), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "0.jpg")));
        Assert.Equal(new ImageSize(60, 90), PageImagePipeline.ReadOrientedSize(Path.Combine(pages.Path, "1.jpg")));
    }

    #endregion
}
