using System.Text;
using System.ComponentModel;
using System.IO.Compression;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Alerts;

namespace Lpubsppop01.EBookBuilder;

public partial class MainPage : ContentPage
{
	#region Constructor

	readonly IMyFolderPicker folderPicker;

	public MainPage(IMyFolderPicker folderPicker)
	{
		InitializeComponent();

		this.folderPicker = folderPicker;

		BindingContext = MainWorkData.Current;
		MainWorkData.Current.LoadJPEGFileItems();
		MainWorkData.Current.PropertyChanged += WorkData_PropertyChanged;
	}

	#endregion

	#region Event Handlers

	// void Window_Loaded(object sender, EventArgs e)
	// {
	// 	Left = Properties.Settings.Default.MainWindow_Left;
	// 	Top = Properties.Settings.Default.MainWindow_Top;
	// 	Width = Properties.Settings.Default.MainWindow_Width;
	// 	Height = Properties.Settings.Default.MainWindow_Height;
	// 	WindowState = Properties.Settings.Default.MainWindow_WindowState;
	// }

	// void Window_Closed(object sender, EventArgs e)
	// {
	// 	Properties.Settings.Default.MainWindow_Left = Left;
	// 	Properties.Settings.Default.MainWindow_Top = Top;
	// 	Properties.Settings.Default.MainWindow_Width = Width;
	// 	Properties.Settings.Default.MainWindow_Height = Height;
	// 	if (WindowState != WindowState.Minimized)
	// 	{
	// 		Properties.Settings.Default.MainWindow_WindowState = WindowState;
	// 	}
	// 	Properties.Settings.Default.Save();
	// }

	void WorkData_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "TargetDirectoryPath")
		{
			MainWorkData.Current.LoadJPEGFileItems();
			UpdatePreviewImage();
		}
	}

	async void btnSelectTargetFolder_Click(object sender, EventArgs e)
	{
		var result = await folderPicker.PickFolder();
		if (result == null) return;
		MainWorkData.Current.TargetDirectoryPath = result;
	}

	void btnCheckAll_Click(object sender, EventArgs e)
	{
		foreach (var item in MainWorkData.Current.JPEGFileItems)
		{
			item.IsChecked = true;
		}
	}

	void btnCheckOdd_Click(object sender, EventArgs e)
	{
		int i = 0;
		foreach (var item in MainWorkData.Current.JPEGFileItems)
		{
			bool indexIsEven = (i++ % 2 == 0);
			if (!indexIsEven) continue;
			item.IsChecked = true;
		}
	}

	void btnCheckEven_Click(object sender, EventArgs e)
	{
		int i = 0;
		foreach (var item in MainWorkData.Current.JPEGFileItems)
		{
			bool indexIsOdd = (i++ % 2 == 1);
			if (!indexIsOdd) continue;
			item.IsChecked = true;
		}
	}

	void btnUncheckAll_Click(object sender, EventArgs e)
	{
		foreach (var item in MainWorkData.Current.JPEGFileItems)
		{
			item.IsChecked = false;
		}
	}

	void btnUncheckUpper_Click(object sender, EventArgs e)
	{
		var selectedItem = lstJPEGFileItems.SelectedItem as JPEGFileItem;
		if (selectedItem == null) return;
		foreach (var item in MainWorkData.Current.JPEGFileItems)
		{
			if (item == selectedItem) break;
			item.IsChecked = false;
		}
	}

	void btnUncheckLower_Click(object sender, EventArgs e)
	{
		var selectedItem = lstJPEGFileItems.SelectedItem as JPEGFileItem;
		if (selectedItem == null) return;
		foreach (var item in MainWorkData.Current.JPEGFileItems.Reverse())
		{
			if (item == selectedItem) break;
			item.IsChecked = false;
		}
	}

	void btnRotate90_Click(object sender, EventArgs e)
	{
		RotateCheckedJPEGFiles("90");
	}

	void btnRotate180_Click(object sender, EventArgs e)
	{
		RotateCheckedJPEGFiles("180");
	}

	void btnRotate270_Click(object sender, EventArgs e)
	{
		RotateCheckedJPEGFiles("270");
	}

	void btnDuplicateToNext_Click(object sender, EventArgs e)
	{
		Duplicate(toLast: false);
	}

	void btnDuplicateToLast_Click(object sender, EventArgs e)
	{
		Duplicate(toLast: true);
	}

	void btnMoveToLast_Click(object sender, EventArgs e)
	{
		MoveToLast();
	}

	void btnDelete_Click(object sender, EventArgs e)
	{
		Delete();
	}

	void btnRenameWithSN_Click(object sender, EventArgs e)
	{
		RenameWithSerialNumber();
	}

	void btnBuild_Click(object sender, EventArgs e)
	{
		Build();
	}

	async void btnShowReadme_Click(object sender, EventArgs e)
	{
		var path = "README.md";
		using var stream = await FileSystem.Current.OpenAppPackageFileAsync(path);
		using var reader = new StreamReader(stream);
		var text = await reader.ReadToEndAsync();
		await DisplayAlert("README", text, "OK");
	}

	void lstJPEGFileItems_SelectionChanged(object sender, SelectedItemChangedEventArgs e)
	{
		UpdatePreviewImage();
	}

	#endregion

	#region Preview Image

	void UpdatePreviewImage()
	{
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath)) return;

		var selectedItem = lstJPEGFileItems.SelectedItem as JPEGFileItem;
		if (selectedItem == null)
		{
			ctrlPreviewImage.Source = null;
			return;
		}
		string path = Path.Combine(MainWorkData.Current.TargetDirectoryPath, selectedItem.Filename);
		try
		{
			var imageBytes = MyMauiGraphicsUtility.GetBytes(path);
			var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));
			ctrlPreviewImage.Source = imageSource;
		}
		catch
		{
			ctrlPreviewImage.Source = null;
		}
	}

	#endregion

	#region Edit

	async void RotateCheckedJPEGFiles(string rotDeg)
	{
		// Check that target directory exists
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath))
		{
			await DisplayAlert("Error", "Target directory doesn't exist.", "OK");
			return;
		}

		// Rotate
		string targetDirPath = MainWorkData.Current.TargetDirectoryPath;
		var targetItems = MainWorkData.Current.JPEGFileItems.Where(i => i.IsChecked).ToArray();
		ProgressDialog.ShowDialog((sender, e) =>
		{
			int doneCount = 0;
			object doneCountLock = new object();
			Parallel.ForEach(targetItems, (targetItem) =>
			{
				lock (doneCountLock)
				{
					int percentage = (int)Math.Floor(((double)doneCount / targetItems.Length) * 100);
					string message = string.Format("{0} of {1} ({2}%) Rotated", doneCount, targetItems.Length, percentage);
					sender.ReportProgress(percentage, message);
				}
				string inputFilePath = Path.Combine(targetDirPath, targetItem.Filename);
				var orientation = MyExifOrientationUtility.Read(inputFilePath);
				var rotatedOrientation = orientation.Rotated(rotDeg);
				MyExifOrientationUtility.Write(inputFilePath, rotatedOrientation);
				lock (doneCountLock)
				{
					++doneCount;
				}
			});
			MainThread.BeginInvokeOnMainThread(() =>
			{
				UpdatePreviewImage();
			});
		}, this);
	}

	async void RenameWithSerialNumber()
	{
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath))
		{
			await DisplayAlert("Error", "Target directory doesn't exist.", "OK");
			return;
		}

		string targetDirPath = MainWorkData.Current.TargetDirectoryPath;
		var targetItems = MainWorkData.Current.JPEGFileItems.ToArray();
		ProgressDialog.ShowDialog((sender, e) =>
		{
			string filenameFormat = BuildFilenameFormat(targetItems.Length);
			for (int i = targetItems.Length - 1; i >= 0; --i)
			{
				int percentage = (int)Math.Floor(((double)i / targetItems.Length) * 100);
				string message = string.Format("{0} of {1} ({2}%) Renamed", i, targetItems.Length, percentage);
				sender.ReportProgress(percentage, message);
				string inputFilePath = Path.Combine(targetDirPath, targetItems[i].Filename);
				string outputFilename = string.Format(filenameFormat, i);
				if (outputFilename == targetItems[i].Filename) continue;
				string outputFilePath = Path.Combine(targetDirPath, outputFilename);
				if (File.Exists(outputFilePath))
				{
					outputFilename = "temp_" + outputFilename;
					outputFilePath = Path.Combine(targetDirPath, outputFilename);
				}
				File.Move(inputFilePath, outputFilePath);
				targetItems[i].Filename = outputFilename;
			}
			for (int i = targetItems.Length - 1; i >= 0; --i)
			{
				if (!targetItems[i].Filename.StartsWith("temp_")) continue;
				string inputFilePath = Path.Combine(targetDirPath, targetItems[i].Filename);
				string outputFilename = targetItems[i].Filename.Replace("temp_", "");
				string outputFilePath = Path.Combine(targetDirPath, outputFilename);
				File.Move(inputFilePath, outputFilePath);
				targetItems[i].Filename = outputFilename;
			}
		}, this);
	}

	static string BuildFilenameFormat(int count)
	{
		int digits = 0;
		while (count > 0)
		{
			count /= 10;
			++digits;
		}

		var buf = new StringBuilder();
		buf.Append("{0:");
		for (int i = 0; i < digits; ++i)
		{
			buf.Append("0");
		}
		buf.Append("}.jpg");
		return buf.ToString();
	}

	async void Duplicate(bool toLast)
	{
		// Check that target directory exists
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath))
		{
			await DisplayAlert("Error", "Target directory doesn't exist.", "OK");
			return;
		}

		// Check states
		var checkResult = await CheckSingleActionIsEnabled("duplication");
		if (!checkResult.status) return;
		var targetItem = checkResult.targetItem;
		if (targetItem == null) return;
		var targetItemIndex = checkResult.targetItemIndex;
		if (targetItemIndex < 0) return;

		// Duplicate
		string targetDirPath = MainWorkData.Current.TargetDirectoryPath;
		string srcFilePath = Path.Combine(targetDirPath, targetItem.Filename);
		string copyFilename = "copy.jpg";
		string copyFilePath = Path.Combine(targetDirPath, copyFilename);
		File.Copy(srcFilePath, copyFilePath);
		if (toLast)
		{
			MainWorkData.Current.JPEGFileItems.Add(new JPEGFileItem { Filename = copyFilename });
		}
		else
		{
			MainWorkData.Current.JPEGFileItems.Insert(targetItemIndex + 1, new JPEGFileItem { Filename = copyFilename });
		}
		RenameWithSerialNumber();
	}

	async Task<(bool status, JPEGFileItem? targetItem, int targetItemIndex)>
	CheckSingleActionIsEnabled(string actionName)
	{
		JPEGFileItem? targetItem = null;
		int targetItemIndex = 0;

		// Check selection
		{
			var targetTuples = MainWorkData.Current.JPEGFileItems.Select((v, i) => new { Value = v, Index = i }).Where(t => t.Value.IsChecked).ToArray();
			if (targetTuples.Length != 1)
			{
				var message = string.Format("The number of selection must be one on {0}.", actionName);
				await DisplayAlert("Error", message, "OK");
				return (false, null, 0);
			}
			targetItem = targetTuples.First().Value;
			targetItemIndex = targetTuples.First().Index;
		}

		// Check filenames are serial numbers
		if (!FilenamesAreSerialNumbers)
		{
			await DisplayAlert("Error", "Filenames must be serial numbers.", "OK");
			return (false, null, 0);
		}

		// Select target if not selected
		var selectedItem = lstJPEGFileItems.SelectedItem as JPEGFileItem;
		if (targetItem != selectedItem)
		{
			lstJPEGFileItems.SelectedItem = targetItem;
			UpdatePreviewImage();
		}
		return (true, targetItem, targetItemIndex);
	}

	bool FilenamesAreSerialNumbers
	{
		get
		{
			var allItems = MainWorkData.Current.JPEGFileItems;
			string filenameFormat = BuildFilenameFormat(MainWorkData.Current.JPEGFileItems.Count);
			bool result = allItems.Select((v, i) => new { v, i }).All(t => t.v.Filename == string.Format(filenameFormat, t.i));
			return result;
		}
	}

	async void MoveToLast()
	{
		// Check states
		JPEGFileItem? targetItem;
		int targetItemIndex;
		var checkResult = await CheckSingleActionIsEnabled("move");
		if (!checkResult.status) return;
		targetItem = checkResult.targetItem;
		if (targetItem == null) return;
		targetItemIndex = checkResult.targetItemIndex;
		if (targetItemIndex < 0) return;

		// Move
		MainWorkData.Current.JPEGFileItems.RemoveAt(targetItemIndex);
		MainWorkData.Current.JPEGFileItems.Add(targetItem);
		RenameWithSerialNumber();
	}

	async void Delete()
	{
		// Check that target directory exists
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath))
		{
			await DisplayAlert("Error", "Target directory doesn't exist.", "OK");
			return;
		}

		// Check states
		JPEGFileItem? targetItem;
		int targetItemIndex;
		var checkResult = await CheckSingleActionIsEnabled("delete");
		if (!checkResult.status) return;
		targetItem = checkResult.targetItem;
		if (targetItem == null ) return;
		targetItemIndex = checkResult.targetItemIndex;
		if (targetItemIndex < 0) return;

		// Confirm
		var message = string.Format("Do you really want to delete \"{0}\"?", targetItem.Filename);
		var result = await DisplayAlert("Confirm", message, "Yes", "No");
		if (!result) return;

		// Delete
		MainWorkData.Current.JPEGFileItems.RemoveAt(targetItemIndex);
		string targetPath = Path.Combine(MainWorkData.Current.TargetDirectoryPath, targetItem.Filename);
		File.Delete(targetPath);
	}

	#endregion

	#region Build

	async void Build()
	{
		// Check filenames are serial numbers
		if (!FilenamesAreSerialNumbers)
		{
			await DisplayAlert("Error", "Filenames must be serial numbers.", "OK");
			return;
		}

		// Check that target directory exists
		if (!Directory.Exists(MainWorkData.Current.TargetDirectoryPath))
		{
			await DisplayAlert("Error", "Target directory doesn't exist.", "OK");
			return;
		}


		// Show build dialog
        var buildDialog = new BuildDialog
        {
            BindingContext = BuildWorkData.Current,
            OnOK = async () =>
            {
                await Navigation.PopModalAsync();
                BuildCore();
            }
        };
        await Navigation.PushModalAsync(buildDialog);
	}

    void BuildCore()
    {
        string targetDirPath = MainWorkData.Current.TargetDirectoryPath;
        var targetItems = MainWorkData.Current.JPEGFileItems.ToArray();
        ProgressDialog.ShowDialog((sender, e) =>
        {
            string defaultName = Path.GetFileNameWithoutExtension(MainWorkData.Current.TargetDirectoryPath);
            string tempDirPath = Path.Combine(Path.GetTempPath(), "Lpubsppop01.EBookBuilder.MainPage.Build");
            if (Directory.Exists(tempDirPath))
            {
                Directory.Delete(tempDirPath, recursive: true);
            }
            Directory.CreateDirectory(tempDirPath);
            var tempImagesDirPath = Path.Combine(tempDirPath, "images");
            Directory.CreateDirectory(tempImagesDirPath);
            var tempOutputPath = Path.Combine(FileSystem.CacheDirectory, "output.cbz");
            if (File.Exists(tempOutputPath))
            {
                File.Delete(tempOutputPath);
            }

            int doneCount = 0;
            object doneCountLock = new object();
            Parallel.ForEach(targetItems, (targetItem) =>
            {
                lock (doneCountLock)
                {
                    int percentage = (int)Math.Floor(((double)doneCount / targetItems.Length) * 100);
                    string message = string.Format("{0} of {1} ({2}%) Resized", doneCount, targetItems.Length, percentage);
                    sender.ReportProgress(percentage, message);
                }
                string inputFilePath = Path.Combine(targetDirPath, targetItem.Filename);
                string tempFilePath = Path.Combine(tempImagesDirPath, targetItem.Filename);
                MyMauiGraphicsUtility.Resize(inputFilePath, tempFilePath,
                    BuildWorkData.Current.Width, BuildWorkData.Current.Height);
                lock (doneCountLock)
                {
                    ++doneCount;
                }
            });
            {
                int percentage = 100;
                string message = string.Format("Creating an output file...");
                sender.ReportProgress(percentage, message);
                ZipFile.CreateFromDirectory(tempImagesDirPath, tempOutputPath, CompressionLevel.NoCompression, includeBaseDirectory: false);
                var stream = File.OpenRead(tempOutputPath);

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await FileSaver.Default.SaveAsync($"{defaultName}.cbz", stream);
                });
            }
        }, this);
    }

    #endregion
}

