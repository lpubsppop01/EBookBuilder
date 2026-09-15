namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>Lets the user choose a target folder.</summary>
/// <remarks>
/// The original WPF version called a Windows-only folder picker dialog directly.
/// Here it is handled through an interface so that it can be swapped out.
/// </remarks>
public interface IFolderPicker
{
    /// <summary>Lets the user choose a folder. Returns null if cancelled.</summary>
    Task<string?> PickFolderAsync(string? initialDirectoryPath);
}
