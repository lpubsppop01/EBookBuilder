using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>Folder selection using Avalonia's StorageProvider.</summary>
/// <remarks>
/// On Linux this goes through the XDG desktop portal, and on Windows the OS dialog appears.
/// There is no dependency on a Windows-only API as in the original WPF version.
/// </remarks>
public sealed class StorageProviderFolderPicker : IFolderPicker
{
    readonly Func<TopLevel?> m_TopLevelProvider;

    public StorageProviderFolderPicker(Func<TopLevel?> topLevelProvider) =>
        m_TopLevelProvider = topLevelProvider;

    public async Task<string?> PickFolderAsync(string? initialDirectoryPath)
    {
        var topLevel = m_TopLevelProvider();
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the folder containing the page images",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(topLevel, initialDirectoryPath),
        });

        // Cancelling yields an empty list. Return null in that case.
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
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
            // Even if setting the initial location fails, the selection itself can continue
            return null;
        }
    }
}
