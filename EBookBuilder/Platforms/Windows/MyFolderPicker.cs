using Windows.Storage.Pickers;

namespace Lpubsppop01.EBookBuilder.Platforms.Windows;

public class MyFolderPicker : IMyFolderPicker
{
    public async Task<string?> PickFolder()
    {
        var folderPicker = new FolderPicker();
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window == null) return null;
        var winUIWindow = window.Handler.PlatformView as MauiWinUIWindow;
        if (winUIWindow == null) return null;
        var hwnd = winUIWindow.WindowHandle;
        if (hwnd == IntPtr.Zero) return null;
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

