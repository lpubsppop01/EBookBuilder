using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lpubsppop01.EBookBuilder.App.Views;

namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>Confirmations and notifications using Avalonia windows and the StorageProvider.</summary>
public sealed class DialogService : IDialogService
{
    readonly Func<Window?> m_OwnerProvider;

    public DialogService(Func<Window?> ownerProvider) => m_OwnerProvider = ownerProvider;

    public async Task<bool> ConfirmAsync(string message, string title = "Confirm")
    {
        var owner = m_OwnerProvider();
        var dialog = new MessageDialog(title, message, showsCancel: true);

        if (owner is null) return await dialog.ShowDialog<bool>(new Window());

        dialog.Icon = owner.Icon;
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task AlertAsync(string message, string title = "EBookBuilder")
    {
        var owner = m_OwnerProvider();
        var dialog = new MessageDialog(title, message, showsCancel: false);

        if (owner is null)
        {
            await dialog.ShowDialog<bool>(new Window());
            return;
        }

        dialog.Icon = owner.Icon;
        await dialog.ShowDialog<bool>(owner);
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string? initialDirectoryPath)
    {
        var topLevel = m_OwnerProvider() as TopLevel;
        if (topLevel is null) return null;

        // The suggested name already carries the extension the build is going to write, so both the
        // title and the file types are taken from it rather than from the settings a second time.
        var (title, fileTypes) = DescribeSaveTarget(suggestedFileName);

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(suggestedFileName).TrimStart('.'),
            SuggestedStartLocation = await TryGetFolderAsync(topLevel, initialDirectoryPath),
            FileTypeChoices = fileTypes,
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>Describes what the save picker should offer for a suggested name.</summary>
    /// <param name="suggestedFileName">The name the build is going to write.</param>
    /// <remarks>
    /// Kept separate from the picker itself, which needs a live window and so cannot be tested,
    /// while this can.
    /// </remarks>
    public static (string Title, FilePickerFileType[] Types) DescribeSaveTarget(string suggestedFileName)
    {
        if (Path.GetExtension(suggestedFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ("Choose where to save the PDF",
                [new FilePickerFileType("PDF document") { Patterns = ["*.pdf"] }]);
        }

        return ("Choose where to save the CBZ",
        [
            new FilePickerFileType("Comic Book ZIP") { Patterns = ["*.cbz"] },
            new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] },
        ]);
    }

    static async Task<IStorageFolder?> TryGetFolderAsync(TopLevel topLevel, string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;

        try
        {
            return await topLevel.StorageProvider.TryGetFolderFromPathAsync(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
