using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.App.Views;
using Lpubsppop01.EBookBuilder.Core;

namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>A collection of routines that show windows and return their results.</summary>
public sealed class ShellDialogs : IShellDialogs
{
    readonly Func<Window?> m_OwnerProvider;
    readonly IDialogService m_Dialogs;
    readonly Func<string?> m_TargetDirectoryProvider;

    public ShellDialogs(
        Func<Window?> ownerProvider,
        IDialogService dialogs,
        Func<string?> targetDirectoryProvider)
    {
        m_OwnerProvider = ownerProvider;
        m_Dialogs = dialogs;
        m_TargetDirectoryProvider = targetDirectoryProvider;
    }

    public async Task<bool> ShowCropDialogAsync(CropSettings settings, string previewImagePath)
    {
        var owner = m_OwnerProvider();
        if (owner is null) return false;

        var dialog = new CropDialog(settings, previewImagePath) { Icon = owner.Icon };
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<bool> ShowBuildDialogAsync(BuildSettings settings)
    {
        var owner = m_OwnerProvider();
        if (owner is null) return false;

        var dialog = new BuildDialog(settings, m_Dialogs, m_TargetDirectoryProvider()) { Icon = owner.Icon };
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task RunWithProgressAsync(Func<IProgress<PageProgress>, CancellationToken, Task> work)
    {
        var owner = m_OwnerProvider();
        if (owner is null) return;

        var dialog = new ProgressDialog("Working...", work) { Icon = owner.Icon };
        await dialog.ShowDialog(owner);

        // Exceptions from the work are handed back to the caller here.
        // In the original WPF version the progress dialog swallowed exceptions,
        // so a failure produced nothing and the cause could not be told.
        if (dialog.Error is { } error) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
