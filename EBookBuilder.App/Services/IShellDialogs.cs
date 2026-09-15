using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.Core;

namespace Lpubsppop01.EBookBuilder.App.Services;

/// <summary>Operations that involve windows. Inserted so that the view model does not touch the screen directly.</summary>
public interface IShellDialogs
{
    /// <summary>Shows the crop dialog. True if OK.</summary>
    Task<bool> ShowCropDialogAsync(CropSettings settings, string previewImagePath);

    /// <summary>Shows the build dialog. True if OK.</summary>
    Task<bool> ShowBuildDialogAsync(BuildSettings settings);

    /// <summary>
    /// Runs work while showing the progress dialog.
    /// </summary>
    /// <remarks>The work runs on a background thread. The stop button cancels <c>token</c>.</remarks>
    Task RunWithProgressAsync(Func<IProgress<PageProgress>, CancellationToken, Task> work);
}
