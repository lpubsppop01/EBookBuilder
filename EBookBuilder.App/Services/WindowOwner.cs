using Avalonia.Controls;

namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>
/// A holder for injecting the main window currently being shown in after the fact.
/// </summary>
/// <remarks>
/// Folder selection and the various dialogs need a window, but the view model is
/// already needed by the time the window is created. Going through this holder
/// avoids that circular dependency.
/// </remarks>
public sealed class WindowOwner
{
    /// <summary>The current main window.</summary>
    public Window? Value { get; set; }
}
