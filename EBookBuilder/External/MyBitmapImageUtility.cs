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

        public static BitmapImage Resize(string inputFilePath, int width, int height)
        {
            // Create resized image
            var adjustedDecodeSize = GetAdjustedDecodeSize(inputFilePath, width, height);
            var resizedImage = LoadWithoutLock(inputFilePath, adjustedDecodeSize);
            return resizedImage;
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

        public static void Save(BitmapSource image, string path)
        {
            var extension = Path.GetExtension(path).ToLower();
            BitmapEncoder encoder;
            if (extension == ".jpg" || extension == ".jpeg")
            {
                encoder = new JpegBitmapEncoder();
            }
            else if (extension == ".png")
            {
                encoder = new PngBitmapEncoder();
            }
            else
            {
                throw new InvalidDataException("Unsupported image format.");
            }
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

        public static WriteableBitmap DrawCornerDots(BitmapSource image)
        {
            // Convert to byte array
            var pixelWidth = image.PixelWidth;
            var pixelHeight = image.PixelHeight;
            var bytesPerPixel = image.Format.BitsPerPixel / 8;
            var stride = bytesPerPixel * pixelWidth;
            var pixels = new byte[stride * pixelHeight];
            image.CopyPixels(pixels, stride, 0);

            // Draw corner dots
            var dotsWidth = 2;
            var dotsHeight = 2;
            for (var y = 0; y < pixelHeight; y++)
            {
                for (var x = 0; x < pixelWidth; x++)
                {
                    var xIsInTarget = x < dotsWidth || x >= pixelWidth - dotsWidth;
                    var yIsInTarget = y < dotsHeight || y >= pixelHeight - dotsHeight;
                    if (xIsInTarget & yIsInTarget)
                    {
                        var index = y * stride + x * bytesPerPixel;
                        if (bytesPerPixel > 0)
                            pixels[index] = 0;
                        if (bytesPerPixel > 1)
                            pixels[index + 1] = 0;
                        if (bytesPerPixel > 2)
                            pixels[index + 2] = 0;
                        if (bytesPerPixel > 3)
                            pixels[index + 3] = 255;
                    }
                }
            }

            // Create image from edited byte array
            var borderedImage = new WriteableBitmap(pixelWidth, pixelHeight, image.DpiX, image.DpiY, image.Format, image.Palette);
            borderedImage.WritePixels(new Int32Rect(0, 0, pixelWidth, pixelHeight), pixels, stride, 0);
            return borderedImage;
        }
    }
}
