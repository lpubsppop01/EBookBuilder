using Windows.Storage.Pickers;

namespace Lpubsppop01.EBookBuilder.Platforms.Windows;

public class FolderPicker : IFolderPicker
{
    public async Task<string> PickFolder()
    {
        var folderPicker = new FolderPicker();    
        var folder = await folderPicker.PickSingleFolderAsync();
        return folder?.Path;
    }
}

