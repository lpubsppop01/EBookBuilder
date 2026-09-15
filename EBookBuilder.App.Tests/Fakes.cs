using Lpubsppop01.EBookBuilder.App.Services;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.Core;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>A stand-in for folder selection. Returns the configured value as-is.</summary>
public sealed class FakeFolderPicker : IFolderPicker
{
    public string? Result { get; set; }

    public Task<string?> PickFolderAsync(string? initialDirectoryPath) => Task.FromResult(Result);
}

/// <summary>A stand-in for confirmations and notifications. Records what it was called with.</summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>The notifications that were shown.</summary>
    public List<string> Alerts { get; } = [];

    /// <summary>The confirmation messages that were requested.</summary>
    public List<string> Confirms { get; } = [];

    /// <summary>The answer returned for confirmations.</summary>
    public bool ConfirmResult { get; set; }

    public Task<bool> ConfirmAsync(string message, string title = "Confirm")
    {
        Confirms.Add(message);
        return Task.FromResult(ConfirmResult);
    }

    public Task AlertAsync(string message, string title = "EBookBuilder")
    {
        Alerts.Add(message);
        return Task.CompletedTask;
    }

    public Task<string?> PickSaveFileAsync(string suggestedFileName, string? initialDirectoryPath) =>
        Task.FromResult<string?>(null);
}

/// <summary>A stand-in for showing dialogs. Runs only the work, without actually showing a screen.</summary>
public sealed class FakeShellDialogs : IShellDialogs
{
    /// <summary>The answer returned by the crop dialog.</summary>
    public bool CropResult { get; set; }

    /// <summary>The answer returned by the build dialog.</summary>
    public bool BuildResult { get; set; }

    /// <summary>The settings passed to the crop dialog.</summary>
    public CropSettings? LastCropSettings { get; private set; }

    /// <summary>The settings passed to the build dialog.</summary>
    public BuildSettings? LastBuildSettings { get; private set; }

    /// <summary>The number of times the progress dialog was shown.</summary>
    public int RunCount { get; private set; }

    /// <summary>A cancellation token used to simulate cancellation.</summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public Task<bool> ShowCropDialogAsync(CropSettings settings, string previewImagePath)
    {
        LastCropSettings = settings;
        return Task.FromResult(CropResult);
    }

    public Task<bool> ShowBuildDialogAsync(BuildSettings settings)
    {
        LastBuildSettings = settings;
        return Task.FromResult(BuildResult);
    }

    public Task RunWithProgressAsync(Func<IProgress<PageProgress>, CancellationToken, Task> work)
    {
        ++RunCount;
        return work(new Progress<PageProgress>(), CancellationToken);
    }
}
