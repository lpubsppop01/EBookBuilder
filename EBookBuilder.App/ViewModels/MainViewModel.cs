using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

        // Crop and delete take every checked page as their target, so one or more checks are enough.
        CropCommand = Guarded(CropAsync, () => CanDoCheckedAction);
        DeleteCommand = Guarded(DeleteAsync, () => CanDoCheckedAction);

        DuplicateToNextCommand = Guarded(() => DuplicateAsync(toLast: false), () => CanDoSingleAction);
        DuplicateToLastCommand = Guarded(() => DuplicateAsync(toLast: true), () => CanDoSingleAction);
        MoveToLastCommand = Guarded(MoveToLastAsync, () => CanDoSingleAction);

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
    bool CanDoSingleAction => CheckedIndex >= 0 && CanDoCheckedAction;

    /// <summary>Whether one or more pages are checked and the filenames are serial numbers.</summary>
    bool CanDoCheckedAction => CheckedPages.Count > 0 && PageNaming.AreSerialNumbers(Filenames);

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
        OnPropertyChanged(nameof(CanDoCheckedAction));
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

    /// <summary>
    /// Crops the checked pages with the same margins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The crop dialog shows a single preview image to decide the margins on, so it can only be
    /// opened when every target has the same dimensions. Pages that differ by a few pixels are
    /// still treated as the same size, because that is normal for scanned pages (see
    /// <see cref="CropSizeTolerance"/>); a page of a different format is not accepted. The preview
    /// uses the first checked page.
    /// </para>
    /// <para>
    /// The margins are validated against every target, not only against the previewed page: with
    /// the tolerance above, margins that fit the first page can still be too large for a slightly
    /// smaller one, and that would fail partway through and leave the pages unevenly cropped.
    /// </para>
    /// </remarks>
    async Task CropAsync()
    {
        var targets = CheckedPages.Select(page => page.Filename).ToArray();
        if (targets.Length == 0) return;

        var sizes = targets
            .Select(filename => PageImagePipeline.ReadOrientedSize(Path.Combine(TargetDirectoryPath, filename)))
            .ToArray();
        var sourceSize = sizes[0];

        if (targets.Length > 1 && !sizes.All(size => CropSizeTolerance.AreSameSize(sourceSize, size)))
        {
            await m_Dialogs.AlertAsync(DescribeMixedSizes(targets, sizes), "Cannot crop");
            return;
        }

        var settings = new CropSettings
        {
            SourceSize = sourceSize,
            TargetCount = targets.Length,
            SizeDifference = sizes.Max(size => CropSizeTolerance.Difference(sourceSize, size)),
        };

        var path = Path.Combine(TargetDirectoryPath, targets[0]);
        if (!await m_Shell.ShowCropDialogAsync(settings, path)) return;

        if (!sizes.All(settings.LeavesAreaOn))
        {
            await m_Dialogs.AlertAsync("The margins are too large.", "Cannot crop");
            return;
        }

        await m_Shell.RunWithProgressAsync((progress, token) =>
        {
            PageOperations.CropAll(
                TargetDirectoryPath, targets,
                settings.Left, settings.Top, settings.Right, settings.Bottom,
                m_Settings.JpegQuality, progress, token);
            return Task.CompletedTask;
        });

        UpdatePreviewImage();
        StatusMessage = targets.Length == 1
            ? $"Cropped {targets[0]} to {settings.CropWidth} x {settings.CropHeight}."
            : $"Cropped {targets.Length} pages to {settings.CropWidth} x {settings.CropHeight}.";
    }

    /// <summary>Describes the sizes that were mixed in, grouped by size.</summary>
    /// <remarks>
    /// <para>
    /// Grouping by size rather than listing page by page is what makes the reason readable: it
    /// shows whether the mix is scan variation of a few pixels or a page of a different format.
    /// The filenames are listed for the small groups, to point at the page that stands out.
    /// </para>
    /// <para>
    /// How far each size is from the reference is given in both pixels and percent, so that the
    /// tolerance can be judged against what was actually scanned. The allowed difference is stated
    /// at the end, so that it can be compared with those numbers directly.
    /// </para>
    /// </remarks>
    static string DescribeMixedSizes(IReadOnlyList<string> targets, IReadOnlyList<ImageSize> sizes)
    {
        const int MaxListedNames = 5;

        var reference = sizes[0];
        var lines = new List<string>();

        var groups = sizes
            .Select((size, index) => (Size: size, Filename: targets[index]))
            .GroupBy(entry => entry.Size)
            .OrderByDescending(group => group.Count());

        foreach (var group in groups)
        {
            var count = group.Count();
            var size = group.Key;

            var difference = size == reference
                ? "(reference)"
                : $"({DescribeSizeDifference(reference, size)})";
            var names = count <= MaxListedNames
                ? $" ({string.Join(", ", group.Select(entry => entry.Filename))})"
                : "";

            lines.Add($"{size.Width} x {size.Height} {difference}: {count} page{(count == 1 ? "" : "s")}{names}");
        }

        return $"""
            The checked pages do not all have the same size.

            {string.Join(Environment.NewLine, lines)}

            Cropping needs every target to have the same size.
            A difference of up to {CropSizeTolerance.MinimumPixels} pixels or {CropSizeTolerance.Ratio * 100:0.#}%, whichever is larger, is allowed.
            """;
    }

    /// <summary>Describes how far a size is from the reference, in pixels and percent.</summary>
    static string DescribeSizeDifference(ImageSize reference, ImageSize size)
    {
        var parts = new List<string>();

        AddSizeDifference(parts, size.Width - reference.Width, reference.Width, "wider", "narrower");
        AddSizeDifference(parts, size.Height - reference.Height, reference.Height, "taller", "shorter");

        return string.Join(", ", parts);
    }

    static void AddSizeDifference(
        List<string> parts, int difference, int referenceLength, string larger, string smaller)
    {
        if (difference == 0) return;

        // The percent is relative to the reference length, which is what the tolerance uses.
        var percent = ((double)Math.Abs(difference) / referenceLength * 100)
            .ToString("0.##", CultureInfo.InvariantCulture);

        parts.Add($"{Math.Abs(difference)} pixels / {percent}% {(difference > 0 ? larger : smaller)}");
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
        var checkedPages = CheckedPages;
        var targets = checkedPages.Select(page => page.Filename).ToArray();
        if (targets.Length == 0) return;

        if (!await m_Dialogs.ConfirmAsync(DeleteConfirmation(targets))) return;

        var firstIndex = Pages.IndexOf(checkedPages[0]);
        var selectedWasDeleted = SelectedPage is not null && checkedPages.Contains(SelectedPage);

        foreach (var filename in targets) PageOperations.Delete(TargetDirectoryPath, filename);
        foreach (var page in checkedPages) Pages.Remove(page);

        // Leaving the selection on a page that no longer exists would keep the preview and the
        // selection state pointing at nothing, so it moves to the page that took its place.
        if (selectedWasDeleted)
        {
            SelectedPage = Pages.Count == 0 ? null : Pages[Math.Min(firstIndex, Pages.Count - 1)];
        }

        RefreshCommands();
        UpdatePreviewImage();
        StatusMessage = targets.Length == 1
            ? $"Deleted \"{targets[0]}\". The serial numbers were not closed up."
            : $"Deleted {targets.Length} pages. The serial numbers were not closed up.";
    }

    /// <summary>The confirmation message shown before deleting.</summary>
    /// <remarks>The list of names is capped, so that checking everything does not make the dialog endless.</remarks>
    static string DeleteConfirmation(IReadOnlyList<string> targets)
    {
        if (targets.Count == 1) return $"Do you really want to delete \"{targets[0]}\"?";

        const int MaxListed = 10;

        var listed = string.Join(Environment.NewLine, targets.Take(MaxListed).Select(filename => "  " + filename));
        var rest = targets.Count > MaxListed
            ? $"{Environment.NewLine}  ... and {targets.Count - MaxListed} more"
            : "";

        return $"""
            Do you really want to delete {targets.Count} pages?

            {listed}{rest}
            """;
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
            OutputFilePath = BuildSettings.DefaultOutputFilePath(TargetDirectoryPath, m_Settings.ContainerKind),
            ContainerKind = m_Settings.ContainerKind,
            PdfPageDpi = m_Settings.PdfPageDpi,
            ImageFormatKind = m_Settings.ImageFormatKind,
            SizeKind = m_Settings.SizeKind,
            Width = m_Settings.TargetWidth,
            Height = m_Settings.TargetHeight,
            DrawsCornerDots = m_Settings.DrawsCornerDots,
            JpegQuality = m_Settings.JpegQuality,
        };

        if (!await m_Shell.ShowBuildDialogAsync(settings)) return;

        // Remember these for next time
        m_Settings.ContainerKind = settings.ContainerKind;
        m_Settings.PdfPageDpi = settings.PdfPageDpi;
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
            result = await BookBuilder.BuildAsync(
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
