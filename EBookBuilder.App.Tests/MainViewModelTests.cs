using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.App.ViewModels;

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

    #region Enabling and disabling single page operations

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
    public void CheckingExactlyOneEnablesSinglePageOperations()
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
    public void CheckingASecondPageDisablesSinglePageOperations()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        viewModel.Pages[0].IsChecked = true;
        Assert.True(viewModel.CropCommand.CanExecute(null));

        // It crashed here
        viewModel.Pages[1].IsChecked = true;

        Assert.False(viewModel.CropCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void SinglePageOperationsAreDisabledWithTwoOrMoreChecks()
    {
        using var pages = TestPages.Create(3);
        var viewModel = CreateViewModel(new FakeDialogService());
        viewModel.OpenDirectory(pages.Path);

        // Check everything, then re-query whether the commands can execute
        viewModel.CheckAllCommand.Execute(null);

        Assert.False(viewModel.CropCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
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

    #endregion

    #endregion
}
