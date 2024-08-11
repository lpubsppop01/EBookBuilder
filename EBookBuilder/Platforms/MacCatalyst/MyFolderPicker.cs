using System;
using Foundation;
using MobileCoreServices;
using UIKit;

namespace Lpubsppop01.EBookBuilder.Platforms.MacCatalyst;

public class MyFolderPicker : UIDocumentPickerDelegate, IMyFolderPicker
{
	#region Fields

	TaskCompletionSource<string?>? tcs;

	#endregion

	#region IMyFolderPicker implementation

	public async Task<string?> PickFolder()
	{
		var tcs = new TaskCompletionSource<string?>();
		this.tcs = tcs;

		var contentTypes = new string[] { "public.folder" };
		var mode = UIDocumentPickerMode.Open;
		var pickerVC = new UIDocumentPickerViewController(contentTypes, mode);
		pickerVC.Delegate = this;

		var parentVC = Platform.GetCurrentUIViewController();
		parentVC?.PresentViewController(pickerVC, true, null);

		return await tcs.Task;
	}

	#endregion

	#region UIDocumentPickerDelegate implementation

	public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
	{
		var result = urls?.First()?.Path;
		tcs?.TrySetResult(result);
		tcs = null;
	}

	public override void WasCancelled(UIDocumentPickerViewController controller)
	{
		tcs?.TrySetResult(null);
		tcs = null;
	}

	#endregion
}
