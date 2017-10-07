﻿using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            DataContext = MainWorkData.Current;
            MainWorkData.Current.LoadJPEGFileItems();
            MainWorkData.Current.PropertyChanged += WorkData_PropertyChanged;
        }

        #endregion

        #region Event Handlers

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Left = Properties.Settings.Default.MainWindow_Left;
            Top = Properties.Settings.Default.MainWindow_Top;
            Width = Properties.Settings.Default.MainWindow_Width;
            Height = Properties.Settings.Default.MainWindow_Height;
            WindowState = Properties.Settings.Default.MainWindow_WindowState;
        }

        void Window_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.MainWindow_Left = Left;
            Properties.Settings.Default.MainWindow_Top = Top;
            Properties.Settings.Default.MainWindow_Width = Width;
            Properties.Settings.Default.MainWindow_Height = Height;
            if (WindowState != WindowState.Minimized)
            {
                Properties.Settings.Default.MainWindow_WindowState = WindowState;
            }
            Properties.Settings.Default.Save();
        }

        void WorkData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TargetDirectoryPath")
            {
                MainWorkData.Current.LoadJPEGFileItems();
                UpdatePreviewImage();
            }
        }

        void btnSelectTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog("Select Target Folder");
            dialog.IsFolderPicker = true;
            dialog.DefaultDirectory = MainWorkData.Current.TargetDirectoryPath;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                MainWorkData.Current.TargetDirectoryPath = dialog.FileName;
            }
        }

        void btnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in MainWorkData.Current.JPEGFileItems)
            {
                item.IsChecked = true;
            }
        }

        void btnCheckOdd_Click(object sender, RoutedEventArgs e)
        {
            int i = 0;
            foreach (var item in MainWorkData.Current.JPEGFileItems)
            {
                bool indexIsEven = (i++ % 2 == 0);
                if (!indexIsEven) continue;
                item.IsChecked = true;
            }
        }

        void btnCheckEven_Click(object sender, RoutedEventArgs e)
        {
            int i = 0;
            foreach (var item in MainWorkData.Current.JPEGFileItems)
            {
                bool indexIsOdd = (i++ % 2 == 1);
                if (!indexIsOdd) continue;
                item.IsChecked = true;
            }
        }

        void btnUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in MainWorkData.Current.JPEGFileItems)
            {
                item.IsChecked = false;
            }
        }

        void btnUncheckUpper_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = lstJPEGFileItems.SelectedValue as JPEGFileItem;
            if (selectedItem == null) return;
            foreach (var item in MainWorkData.Current.JPEGFileItems)
            {
                if (item == selectedItem) break;
                item.IsChecked = false;
            }
        }

        void btnUncheckLower_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = lstJPEGFileItems.SelectedValue as JPEGFileItem;
            if (selectedItem == null) return;
            foreach (var item in MainWorkData.Current.JPEGFileItems.Reverse())
            {
                if (item == selectedItem) break;
                item.IsChecked = false;
            }
        }

        void btnRotate90_Click(object sender, RoutedEventArgs e)
        {
            RotateCheckedJPEGFiles("90");
            UpdatePreviewImage();
        }

        void btnRotate180_Click(object sender, RoutedEventArgs e)
        {
            RotateCheckedJPEGFiles("180");
            UpdatePreviewImage();
        }

        void btnRotate270_Click(object sender, RoutedEventArgs e)
        {
            RotateCheckedJPEGFiles("270");
            UpdatePreviewImage();
        }

        void btnDuplicateToNext_Click(object sender, RoutedEventArgs e)
        {
            Duplicate(toLast: false);
        }

        void btnDuplicateToLast_Click(object sender, RoutedEventArgs e)
        {
            Duplicate(toLast: true);
        }

        void btnMoveToLast_Click(object sender, RoutedEventArgs e)
        {
            MoveToLast();
        }

        void btnCrop_Click(object sender, RoutedEventArgs e)
        {
            // TODO
            UpdatePreviewImage();
        }

        void btnRenameWithSN_Click(object sender, RoutedEventArgs e)
        {
            RenameWithSerialNumber();
        }

        void btnBuild_Click(object sender, RoutedEventArgs e)
        {
            Build();
        }

        void lstJPEGFileItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreviewImage();
        }

        #endregion

        #region Preview Image

        void UpdatePreviewImage()
        {
            var selectedItem = lstJPEGFileItems.SelectedValue as JPEGFileItem;
            if (selectedItem == null)
            {
                ctrlPreviewImage.Source = null;
                return;
            }
            string path = Path.Combine(MainWorkData.Current.TargetDirectoryPath, selectedItem.Filename);
            try
            {
                // Use WriteableBitmap to avoid lock file
                WriteableBitmap bitmap;
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                {
                    bitmap = new WriteableBitmap(BitmapFrame.Create(ms));
                }
                ctrlPreviewImage.Source = bitmap;
            }
            catch
            {
                ctrlPreviewImage.Source = null;
            }
        }

        #endregion

        #region Edit

        void RotateCheckedJPEGFiles(string rotDeg)
        {
            // Check jpegtran.exe
            if (!JpegTran.Current.IsEnabled)
            {
                MessageBox.Show("jpegtran.exe is not found in PATH.");
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
                    string outputFilePath = Path.GetTempFileName();
                    JpegTran.Current.Rotate(inputFilePath, outputFilePath, rotDeg);
                    File.Delete(inputFilePath);
                    File.Move(outputFilePath, inputFilePath);
                    lock (doneCountLock)
                    {
                        ++doneCount;
                    }
                });
            }, this);
        }

        void RenameWithSerialNumber()
        {
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

        void Duplicate(bool toLast)
        {
            // Check states
            JPEGFileItem targetItem;
            int targetItemIndex;
            if (!CheckSingleActionIsEnabled("duplication", out targetItem, out targetItemIndex)) return;

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

        bool CheckSingleActionIsEnabled(string actionName, out JPEGFileItem targetItem, out int targetItemIndex)
        {
            targetItem = null;
            targetItemIndex = 0;

            // Check selection
            {
                var targetTuples = MainWorkData.Current.JPEGFileItems.Select((v, i) => new { Value = v, Index = i }).Where(t => t.Value.IsChecked).ToArray();
                if (targetTuples.Length != 1)
                {
                    MessageBox.Show(string.Format("The number of selection must be one on {0}.", actionName));
                    return false;
                }
                targetItem = targetTuples.First().Value;
                targetItemIndex = targetTuples.First().Index;
            }

            // Check filenames are serial numbers
            if (!FilenamesAreSerialNumbers)
            {
                MessageBox.Show("Filenames must be serial numbers.");
                return false;
            }
            return true;
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

        void MoveToLast()
        {
            // Check states
            JPEGFileItem targetItem;
            int targetItemIndex;
            if (!CheckSingleActionIsEnabled("move", out targetItem, out targetItemIndex)) return;

            // Move
            MainWorkData.Current.JPEGFileItems.RemoveAt(targetItemIndex);
            MainWorkData.Current.JPEGFileItems.Add(targetItem);
            RenameWithSerialNumber();
        }

        #endregion

        #region Build

        void Build()
        {
            // Check magick.exe
            if (!ImageMagick.Current.IsEnabled)
            {
                MessageBox.Show("magick.exe is not found in PATH.");
                return;
            }

            // Check filenames are serial numbers
            if (!FilenamesAreSerialNumbers)
            {
                MessageBox.Show("Filenames must be serial numbers.");
                return;
            }

            // Show build dialog
            BuildWorkData.Current.OutputFilePath = MainWorkData.Current.TargetDirectoryPath + ".cbz";
            var buildDialog = new BuildDialog
            {
                DataContext = BuildWorkData.Current,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (!(buildDialog.ShowDialog() ?? false)) return;

            // Build
            string targetDirPath = MainWorkData.Current.TargetDirectoryPath;
            var targetItems = MainWorkData.Current.JPEGFileItems.ToArray();
            ProgressDialog.ShowDialog((sender, e) =>
            {
                string tempDirName = Path.GetFileNameWithoutExtension(BuildWorkData.Current.OutputFilePath);
                string tempDirPath = Path.Combine(Path.GetTempPath(), "lpubsppop01.EBookBuilder", tempDirName);
                if (Directory.Exists(tempDirPath))
                {
                    Directory.Delete(tempDirPath, recursive: true);
                }
                Directory.CreateDirectory(tempDirPath);

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
                    string tempFilePath = Path.Combine(tempDirPath, targetItem.Filename);
                    string size = string.Format("{0}x{1}", BuildWorkData.Current.Width, BuildWorkData.Current.Height);
                    ImageMagick.Current.Resize(inputFilePath, tempFilePath, size);
                    lock (doneCountLock)
                    {
                        ++doneCount;
                    }
                });
                {
                    int percentage = 100;
                    string message = string.Format("Creating {0}", Path.GetFileName(BuildWorkData.Current.OutputFilePath));
                    sender.ReportProgress(percentage, message);
                    if (File.Exists(BuildWorkData.Current.OutputFilePath))
                    {
                        File.Delete(BuildWorkData.Current.OutputFilePath);
                    }
                    ZipFile.CreateFromDirectory(tempDirPath, BuildWorkData.Current.OutputFilePath, CompressionLevel.NoCompression, includeBaseDirectory: false);
                }
            }, this);
        }

        #endregion
    }
}
