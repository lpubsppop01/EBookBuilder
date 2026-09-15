namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>Confirmations and notifications for the user.</summary>
/// <remarks>
/// The original WPF version called <c>MessageBox.Show</c> directly from various places,
/// which coupled the logic tightly to the UI. Here it can be swapped out.
/// </remarks>
public interface IDialogService
{
    /// <summary>Asks a yes/no question.</summary>
    Task<bool> ConfirmAsync(string message, string title = "Confirm");

    /// <summary>Notifies the user.</summary>
    Task AlertAsync(string message, string title = "EBookBuilder");

    /// <summary>Lets the user choose where to save a file. Null if cancelled.</summary>
    Task<string?> PickSaveFileAsync(string suggestedFileName, string? initialDirectoryPath);
}
