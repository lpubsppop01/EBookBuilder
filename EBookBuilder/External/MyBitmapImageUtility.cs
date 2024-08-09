using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace lpubsppop01.EBookBuilder.External
{
    static class MyBitmapImageUtility
    {
        public static BitmapImage LoadWithoutLock(string path, (int width, int height)? size = null)
        {
            // Use CacheOption and StreamSource to avoid lock file
            var image = new BitmapImage();
            var stream = File.OpenRead(path);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.Rotation = MyExifOrientationUtility.Read(path).ToRotation();
            if (size.HasValue)
            {
                image.DecodePixelWidth = size.Value.width;
                image.DecodePixelHeight = size.Value.height;
            }
            image.EndInit();
            stream.Close();
            return image;
        }

        public static void Resize(string inputFilePath, string outputFilePath, int width, int height)
        {
            // Create resized image
            var adjustedDecodeSize = GetAdjustedDecodeSize(inputFilePath, width, height);
            var resizedImage = LoadWithoutLock(inputFilePath, adjustedDecodeSize);

            // Save resized image
            Save(resizedImage, outputFilePath);
        }

        static (int width, int height) GetAdjustedDecodeSize(string path, int width, int height)
        {
            // Load image
            var image = LoadWithoutLock(path);

            // Adjust passed size to keep aspect ratio
            var imageAspectRatio = (double)image.PixelWidth / image.PixelHeight;
            var passedAspectRatio = (double)width / height;
            var adjustedWidth = width;
            var adjustedHeight = height;
            if (imageAspectRatio > passedAspectRatio)
            {
                adjustedHeight = (int)(width / imageAspectRatio);
            }
            else
            {
                adjustedWidth = (int)(height * imageAspectRatio);
            }

            // Rotate size if needed
            if (image.Rotation == Rotation.Rotate90 || image.Rotation == Rotation.Rotate270)
            {
                var temp = adjustedWidth;
                adjustedWidth = adjustedHeight;
                adjustedHeight = temp;
            }
            return (adjustedWidth, adjustedHeight);
        }

        static void Save(BitmapSource image, string path)
        {
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        public static void Crop(string inputFilePath, string outputFilePath, int width, int height, int left, int top)
        {
            // Create cropped image
            var image = LoadWithoutLock(inputFilePath);
            var rect = new Int32Rect(left, top, width, height);
            var croppedImage = new CroppedBitmap(image, rect);

            // Save cropped image
            Save(croppedImage, outputFilePath);
        }
    }
}
