using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using Lpubsppop01.EBookBuilder.App.Imaging;
using Lpubsppop01.EBookBuilder.App.Services;
using Lpubsppop01.EBookBuilder.App.Settings;
using Lpubsppop01.EBookBuilder.Core;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>The state and operations of the main screen.</summary>
/// <remarks>
/// In the original WPF version all of the processing lived in the window's code-behind.
/// Here file operations are left to <see cref="Lpubsppop01.EBookBuilder.Core"/> and the
/// screen-related parts to <see cref="IShellDialogs"/>, and this class only assembles them.
/// </remarks>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>The maximum preview size. Images larger than this are not loaded.</summary>
    static readonly ImageSize PreviewMaxSize = new(1600, 1600);

    readonly IFolderPicker m_FolderPicker;
    readonly IDialogService m_Dialogs;
    readonly IShellDialogs m_Shell;
    readonly AppSettings m_Settings;

    string m_TargetDirectoryPath = "";
    PageItem? m_SelectedPage;
    WriteableBitmap? m_PreviewImage;
    string m_StatusMessage = "";

    public MainViewModel(
        IFolderPicker folderPicker,
        IDialogService dialogs,
        IShellDialogs shell,
        AppSettings settings)
    {
        m_FolderPicker = folderPicker;
        m_Dialogs = dialogs;
        m_Shell = shell;
        m_Settings = settings;

        SelectFolderCommand = Guarded(SelectFolderAsync);
        CheckAllCommand = Guarded(() => SetChecked(_ => true));
        CheckOddCommand = Guarded(() => SetCheckedByIndex(i => i % 2 == 0));
        CheckEvenCommand = Guarded(() => SetCheckedByIndex(i => i % 2 == 1));
        UncheckAllCommand = Guarded(() => SetChecked(_ => false));
        UncheckUpperCommand = Guarded(() => SetCheckedAroundSelected(upper: true));
        UncheckLowerCommand = Guarded(() => SetCheckedAroundSelected(upper: false));

        Rotate90Command = Guarded(() => RotateAsync(RotationAmount.Deg90), () => CanRotate);
        Rotate180Command = Guarded(() => RotateAsync(RotationAmount.Deg180), () => CanRotate);
        Rotate270Command = Guarded(() => RotateAsync(RotationAmount.Deg270), () => CanRotate);

        RenameCommand = Guarded(RenameAsync, () => Pages.Count > 0);
        CropCommand = Guarded(CropAsync, () => CanDoSingleAction);
        DuplicateToNextCommand = Guarded(() => DuplicateAsync(toLast: false), () => CanDoSingleAction);
        DuplicateToLastCommand = Guarded(() => DuplicateAsync(toLast: true), () => CanDoSingleAction);
        MoveToLastCommand = Guarded(MoveToLastAsync, () => CanDoSingleAction);
        DeleteCommand = Guarded(DeleteAsync, () => CanDoSingleAction);

        // A build targets the whole folder, so the number of checked pages does not matter.
        // Whether the filenames are serial numbers is also reported at run time
        // (otherwise there is no way to tell why the button cannot be pressed).
        BuildCommand = Guarded(BuildAsync, () => Pages.Count > 0);
    }

    #region Command creation

    /// <summary>
    /// Creates a command wrapped so that exceptions do not escape.
    /// </summary>
    /// <remarks>
    /// If an exception escapes while a command is running, it reaches Avalonia's event loop
    /// and the app crashes. It is caught here and its content reported, so that a single
    /// button press does not crash the app.
    /// </remarks>
    AsyncRelayCommand Guarded(Func<Task> body, Func<bool>? canExecute = null) =>
        new(() => GuardAsync(body), canExecute);

    /// <inheritdoc cref="Guarded(Func{Task}, Func{bool}?)"/>
    AsyncRelayCommand Guarded(Action body, Func<bool>? canExecute = null) =>
        new(() => GuardAsync(body), canExecute);

    async Task GuardAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            await m_Dialogs.AlertAsync(ex.Message, "Error");
        }
    }

    async Task GuardAsync(Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            await m_Dialogs.AlertAsync(ex.Message, "Error");
        }
    }

    #endregion

    #region State

    /// <summary>The list of pages. The order here is the page order.</summary>
    public ObservableCollection<PageItem> Pages { get; } = [];

    /// <summary>The path of the target folder.</summary>
    public string TargetDirectoryPath
    {
        get => m_TargetDirectoryPath;
        private set
        {
            if (!SetProperty(ref m_TargetDirectoryPath, value)) return;
            OnPropertyChanged(nameof(HasTargetDirectory));
            LoadPages();
        }
    }

    /// <summary>Whether the target folder has been determined.</summary>
    public bool HasTargetDirectory => !string.IsNullOrEmpty(TargetDirectoryPath);

    /// <summary>The currently selected page.</summary>
    public PageItem? SelectedPage
    {
        get => m_SelectedPage;
        set
        {
            if (!SetProperty(ref m_SelectedPage, value)) return;
            OnPropertyChanged(nameof(HasSelectedPage));
            UpdatePreviewImage();
            RefreshCommands();
        }
    }

    /// <summary>Whether a page is selected.</summary>
    public bool HasSelectedPage => SelectedPage is not null;

    /// <summary>The preview image.</summary>
    public WriteableBitmap? PreviewImage
    {
        get => m_PreviewImage;
        private set => SetProperty(ref m_PreviewImage, value);
    }

    /// <summary>The status description shown at the bottom of the screen.</summary>
    public string StatusMessage
    {
        get => m_StatusMessage;
        private set => SetProperty(ref m_StatusMessage, value);
    }

    #endregion

    #region Commands

    public AsyncRelayCommand SelectFolderCommand { get; }
    public AsyncRelayCommand CheckAllCommand { get; }
    public AsyncRelayCommand CheckOddCommand { get; }
    public AsyncRelayCommand CheckEvenCommand { get; }
    public AsyncRelayCommand UncheckAllCommand { get; }
    public AsyncRelayCommand UncheckUpperCommand { get; }
    public AsyncRelayCommand UncheckLowerCommand { get; }
    public AsyncRelayCommand Rotate90Command { get; }
    public AsyncRelayCommand Rotate180Command { get; }
    public AsyncRelayCommand Rotate270Command { get; }
    public AsyncRelayCommand RenameCommand { get; }
    public AsyncRelayCommand CropCommand { get; }
    public AsyncRelayCommand DuplicateToNextCommand { get; }
    public AsyncRelayCommand DuplicateToLastCommand { get; }
    public AsyncRelayCommand MoveToLastCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand BuildCommand { get; }

    #endregion

    #region Derived state

    /// <summary>The filenames in their current order.</summary>
    IReadOnlyList<string> Filenames => Pages.Select(page => page.Filename).ToArray();

    /// <summary>The pages that are checked.</summary>
    IReadOnlyList<PageItem> CheckedPages => Pages.Where(page => page.IsChecked).ToArray();

    /// <summary>The position of the single page checked as the target of the operation. -1 if it is not uniquely determined.</summary>
    /// <remarks>
    /// Also returns -1 when two or more are checked (the target of the operation is not determined).
    /// <c>Enumerable.SingleOrDefault</c> cannot be used because it throws with two or more.
    /// </remarks>
    int CheckedIndex => PageSelection.IndexOfSingleChecked(Pages, page => page.IsChecked);

    /// <summary>Whether exactly one page is checked and the filenames are serial numbers.</summary>
    bool CanDoSingleAction => CheckedIndex >= 0 && PageNaming.AreSerialNumbers(Filenames);

    bool CanRotate => CheckedPages.Count > 0;

    #endregion

    #region Loading pages and the preview

    /// <summary>Lets the user choose the target folder.</summary>
    async Task SelectFolderAsync()
    {
        var picked = await m_FolderPicker.PickFolderAsync(m_Settings.LastTargetDirectoryPath);
        if (picked is null) return;

        TargetDirectoryPath = picked;
        m_Settings.LastTargetDirectoryPath = picked;
        m_Settings.Save();
    }

    /// <summary>Loads the given target folder (used from the startup arguments and tests).</summary>
    public void OpenDirectory(string directoryPath) => TargetDirectoryPath = directoryPath;

    void LoadPages()
    {
        Pages.Clear();

        if (!PageFolder.Exists(TargetDirectoryPath))
        {
            SelectedPage = null;
            StatusMessage = "Select a target folder.";
            RefreshCommands();
            return;
        }

        foreach (var filename in PageFolder.EnumeratePageFilenames(TargetDirectoryPath))
        {
            var page = new PageItem { Filename = filename };

            // Clicking a checkbox in the list does not go through a command,
            // so unless it is picked up here the buttons' enabled state is not refreshed.
            page.PropertyChanged += OnPagePropertyChanged;

            Pages.Add(page);
        }

        SelectedPage = Pages.FirstOrDefault();
        StatusMessage = Pages.Count == 0
            ? "No JPEG files were found."
            : $"{Pages.Count} pages.";
        RefreshCommands();
    }

    /// <summary>
    /// Re-queries whether the operations are enabled when a page's state changes.
    /// </summary>
    /// <remarks>
    /// The checked state affects how many targets there are and the filename affects whether
    /// the files are serial numbers, so both are watched.
    /// </remarks>
    void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PageItem.IsChecked) or nameof(PageItem.Filename)) RefreshCommands();
    }

    /// <summary>Reloads the selected page and reflects it in the preview.</summary>
    public void UpdatePreviewImage()
    {
        var old = PreviewImage;
        PreviewImage = SelectedPagePath is { } path
            ? BitmapInterop.LoadPreview(path, PreviewMaxSize)
            : null;
        old?.Dispose();
    }

    string? SelectedPagePath => SelectedPage is null
        ? null
        : Path.Combine(TargetDirectoryPath, SelectedPage.Filename);

    void RefreshCommands()
    {
        foreach (var command in AllCommands) command.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanDoSingleAction));
    }

    IEnumerable<IRelayCommand> AllCommands =>
    [
        Rotate90Command, Rotate180Command, Rotate270Command,
        RenameCommand, CropCommand,
        DuplicateToNextCommand, DuplicateToLastCommand,
        MoveToLastCommand, DeleteCommand, BuildCommand,
    ];

    #endregion

    #region Check operations

    void SetChecked(Func<PageItem, bool> selector)
    {
        foreach (var page in Pages) page.IsChecked = selector(page);
        RefreshCommands();
    }

    void SetCheckedByIndex(Func<int, bool> selector)
    {
        for (var i = 0; i < Pages.Count; ++i) Pages[i].IsChecked = selector(i);
        RefreshCommands();
    }

    /// <summary>
    /// Unchecks the pages above (or below) the selected page.
    /// </summary>
    /// <remarks>Used to clear an already-checked range at once when looking for a missing page.</remarks>
    void SetCheckedAroundSelected(bool upper)
    {
        var selectedIndex = SelectedPage is null ? -1 : Pages.IndexOf(SelectedPage);
        if (selectedIndex < 0) return;

        var range = upper
            ? Enumerable.Range(0, selectedIndex)
            : Enumerable.Range(selectedIndex + 1, Pages.Count - selectedIndex - 1);

        foreach (var index in range) Pages[index].IsChecked = false;
        RefreshCommands();
    }

    #endregion

    #region Editing

    async Task RotateAsync(RotationAmount amount)
    {
        var targets = CheckedPages.Select(page => page.Filename).ToArray();
        if (targets.Length == 0) return;

        await m_Shell.RunWithProgressAsync((progress, token) =>
            PageOperations.RotateAllAsync(TargetDirectoryPath, targets, amount, progress, token));

        UpdatePreviewImage();
        StatusMessage = $"Rotated {targets.Length} pages (the orientation tags were only rewritten).";
    }

    async Task RenameAsync()
    {
        var filenames = Filenames;
        IReadOnlyList<string>? renamed = null;

        await m_Shell.RunWithProgressAsync((progress, token) =>
        {
            renamed = PageOperations.RenameWithSerialNumbers(TargetDirectoryPath, filenames, progress, token);
            return Task.CompletedTask;
        });

        if (renamed is null) return;
        ApplyFilenames(renamed);
        UpdatePreviewImage();
        StatusMessage = $"Renamed {renamed.Count} pages to serial numbers.";
    }

    async Task CropAsync()
    {
        var index = CheckedIndex;
        if (index < 0) return;

        var filename = Pages[index].Filename;
        var path = Path.Combine(TargetDirectoryPath, filename);
        var settings = new CropSettings { SourceSize = PageImagePipeline.ReadOrientedSize(path) };

        if (!await m_Shell.ShowCropDialogAsync(settings, path)) return;
        if (!settings.IsValid)
        {
            await m_Dialogs.AlertAsync("The margins are too large.", "Cannot crop");
            return;
        }

        await m_Shell.RunWithProgressAsync((progress, token) =>
        {
            PageOperations.Crop(
                TargetDirectoryPath, filename,
                settings.Left, settings.Top, settings.Right, settings.Bottom,
                m_Settings.JpegQuality);
            return Task.CompletedTask;
        });

        UpdatePreviewImage();
        StatusMessage = $"Cropped {filename} to {settings.CropWidth} x {settings.CropHeight}.";
    }

    async Task DuplicateAsync(bool toLast)
    {
        var index = CheckedIndex;
        if (index < 0) return;

        var filenames = Filenames;
        IReadOnlyList<string>? renamed = null;

        await m_Shell.RunWithProgressAsync((progress, token) =>
        {
            renamed = PageOperations.Duplicate(TargetDirectoryPath, filenames, index, toLast, progress, token);
            return Task.CompletedTask;
        });

        if (renamed is null) return;

        // Add the duplicated page to the list before aligning the filenames.
        var copy = new PageItem();
        if (toLast) Pages.Add(copy);
        else Pages.Insert(index + 1, copy);

        ApplyFilenames(renamed);
        UpdatePreviewImage();
        StatusMessage = $"Duplicated {Pages[index].Filename}.";
    }

    async Task MoveToLastAsync()
    {
        var index = CheckedIndex;
        if (index < 0) return;

        var filenames = Filenames;
        IReadOnlyList<string>? renamed = null;

        await m_Shell.RunWithProgressAsync((progress, token) =>
        {
            renamed = PageOperations.MoveToLast(TargetDirectoryPath, filenames, index, progress, token);
            return Task.CompletedTask;
        });

        if (renamed is null) return;

        var moved = Pages[index];
        Pages.RemoveAt(index);
        Pages.Add(moved);

        ApplyFilenames(renamed);
        UpdatePreviewImage();
        StatusMessage = $"Moved {moved.Filename} to the end.";
    }

    async Task DeleteAsync()
    {
        var index = CheckedIndex;
        if (index < 0) return;

        var filename = Pages[index].Filename;
        if (!await m_Dialogs.ConfirmAsync($"Do you really want to delete \"{filename}\"?")) return;

        PageOperations.Delete(TargetDirectoryPath, filename);
        Pages.RemoveAt(index);

        RefreshCommands();
        UpdatePreviewImage();
        StatusMessage = $"Deleted \"{filename}\". The serial numbers were not closed up.";
    }

    /// <summary>Replaces only the filenames. The page order is decided by the caller.</summary>
    void ApplyFilenames(IReadOnlyList<string> filenames)
    {
        for (var i = 0; i < Pages.Count && i < filenames.Count; ++i)
        {
            Pages[i].Filename = filenames[i];
        }
        RefreshCommands();
    }

    #endregion

    #region Build

    async Task BuildAsync()
    {
        if (!PageNaming.AreSerialNumbers(Filenames))
        {
            await m_Dialogs.AlertAsync("The filenames are not serial numbers. Rename them to serial numbers first.");
            return;
        }

        var settings = new BuildSettings
        {
            OutputFilePath = TargetDirectoryPath + ".cbz",
            ImageFormatKind = m_Settings.ImageFormatKind,
            SizeKind = m_Settings.SizeKind,
            Width = m_Settings.TargetWidth,
            Height = m_Settings.TargetHeight,
            DrawsCornerDots = m_Settings.DrawsCornerDots,
            JpegQuality = m_Settings.JpegQuality,
        };

        if (!await m_Shell.ShowBuildDialogAsync(settings)) return;

        // Remember these for next time
        m_Settings.ImageFormatKind = settings.ImageFormatKind;
        m_Settings.SizeKind = settings.SizeKind;
        m_Settings.TargetWidth = settings.Width;
        m_Settings.TargetHeight = settings.Height;
        m_Settings.DrawsCornerDots = settings.DrawsCornerDots;
        m_Settings.JpegQuality = settings.JpegQuality;
        m_Settings.Save();

        var filenames = Filenames;
        BuildResult? result = null;

        await m_Shell.RunWithProgressAsync(async (progress, token) =>
        {
            result = await CbzBuilder.BuildAsync(
                TargetDirectoryPath, filenames, settings.ToBuildOptions(), progress, token);
        });

        if (result is not { } built) return;

        StatusMessage = $"Created {Path.GetFileName(built.OutputFilePath)}.";
        await m_Dialogs.AlertAsync(
            $"""
            Created.

            {built.OutputFilePath}

            {built.PageCount} pages
            Not re-encoded: {built.CopiedCount} pages
            Re-encoded: {built.ReEncodedCount} pages
            """);
    }

    #endregion
}
