using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;
using static Lpubsppop01.EBookBuilder.Core.Tests.TestSupport.TestImages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Imaging;

/// <summary>
/// Verification of the image pipeline.
/// </summary>
/// <remarks>
/// The test images are red on the left half and blue on the right half, so the results of
/// rotation and cropping can be judged by which area is red and which is blue.
/// </remarks>
public class PageImagePipelineTests
{
    #region FitSize

    [Fact]
    public void PortraitImageIsConstrainedByWidth()
    {
        // Fit 1000x1500 (ratio 0.667) into 600x1024 (ratio 0.586) -> the width limit is hit first
        var actual = PageImagePipeline.FitSize(new ImageSize(1000, 1500), new ImageSize(600, 1024));
        Assert.Equal(new ImageSize(600, 900), actual);
    }

    [Fact]
    public void LandscapeImageIsConstrainedByHeight()
    {
        // Fit 2000x1000 (ratio 2.0) into 600x1024 -> the height limit is hit first
        var actual = PageImagePipeline.FitSize(new ImageSize(2000, 1000), new ImageSize(600, 1024));
        Assert.Equal(new ImageSize(600, 300), actual);
    }

    [Fact]
    public void SquareImageIsConstrainedByWidth()
    {
        var actual = PageImagePipeline.FitSize(new ImageSize(1000, 1000), new ImageSize(600, 1024));
        Assert.Equal(new ImageSize(600, 600), actual);
    }

    [Fact]
    public void FittedSizeDoesNotOverflowTheTargetBox()
    {
        var target = new ImageSize(600, 1024);
        foreach (var (w, h) in new[] { (3000, 1000), (1000, 3000), (1000, 1000), (1234, 5678) })
        {
            var actual = PageImagePipeline.FitSize(new ImageSize(w, h), target);
            Assert.True(actual.Width <= target.Width, $"{w}x{h}: width overflowed");
            Assert.True(actual.Height <= target.Height, $"{w}x{h}: height overflowed");
        }
    }

    [Fact]
    public void DoesNotEnlargeWhenTargetBoxIsLargerThanSource()
    {
        // The original WPF version enlarged here, but this port does not (an intentional difference).
        var source = new ImageSize(400, 400);
        var actual = PageImagePipeline.FitSize(source, new ImageSize(600, 1024));
        Assert.Equal(source, actual);
    }

    [Fact]
    public void DoesNotEnlargePortraitSourceWhenTargetBoxIsLarger()
    {
        var source = new ImageSize(100, 1000);
        var actual = PageImagePipeline.FitSize(source, new ImageSize(600, 1024));
        Assert.Equal(source, actual);
    }

    [Fact]
    public void SizeEqualToSourceIsReturnedUnchanged()
    {
        var source = new ImageSize(600, 900);
        Assert.Equal(source, PageImagePipeline.FitSize(source, new ImageSize(600, 1024)));
    }

    [Fact]
    public void ExtremelyThinImageKeepsAtLeastOnePixel()
    {
        var actual = PageImagePipeline.FitSize(new ImageSize(10000, 1), new ImageSize(600, 1024));
        Assert.Equal(1, actual.Height);
        Assert.Equal(600, actual.Width);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void InvalidSourceSizeThrows(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PageImagePipeline.FitSize(new ImageSize(width, height), new ImageSize(600, 1024)));
    }

    #endregion

    #region Decoding and EXIF rotation

    [Fact]
    public void ImageWithoutRotationTagIsDecodedAsIs()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        Assert.Equal(80, bitmap.Width);
        Assert.Equal(120, bitmap.Height);
        Assert.True(IsRed(bitmap.GetPixel(10, 60)), "left side should be red");
        Assert.True(IsBlue(bitmap.GetPixel(70, 60)), "right side should be blue");
    }

    [Fact]
    public void Exif90DegreeRotationTagActuallyRotatesPixels()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        // Rotating 90 degrees clockwise brings the original left edge (red) to the top edge
        Assert.Equal(120, bitmap.Width);
        Assert.Equal(80, bitmap.Height);
        Assert.True(IsRed(bitmap.GetPixel(60, 10)), "upper half should be red");
        Assert.True(IsBlue(bitmap.GetPixel(60, 70)), "lower half should be blue");
    }

    [Fact]
    public void Exif270DegreeRotationTagRotatesPixelsTheOtherWay()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate270CW);

        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        // Rotating 270 degrees clockwise brings the original left edge (red) to the bottom edge
        Assert.Equal(120, bitmap.Width);
        Assert.Equal(80, bitmap.Height);
        Assert.True(IsBlue(bitmap.GetPixel(60, 10)), "upper half should be blue");
        Assert.True(IsRed(bitmap.GetPixel(60, 70)), "lower half should be red");
    }

    [Fact]
    public void Exif180DegreeRotationTagSwapsLeftAndRight()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate180);

        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        Assert.Equal(80, bitmap.Width);
        Assert.Equal(120, bitmap.Height);
        Assert.True(IsBlue(bitmap.GetPixel(10, 60)), "left side should be blue");
        Assert.True(IsRed(bitmap.GetPixel(70, 60)), "right side should be red");
    }

    [Fact]
    public void ExifRotationIsAppliedWhenDecodingAtSmallerSize()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        // After rotation it is 120x80. Fit into 60x40.
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path, new ImageSize(60, 40));

        Assert.Equal(60, bitmap.Width);
        Assert.Equal(40, bitmap.Height);
        Assert.True(IsRed(bitmap.GetPixel(30, 5)), "upper half should be red");
        Assert.True(IsBlue(bitmap.GetPixel(30, 35)), "lower half should be blue");
    }

    [Fact]
    public void DecodedImageDoesNotExceedTheTargetBox()
    {
        using var jpeg = TempJpeg.Create(800, 1200);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path, new ImageSize(200, 300));

        Assert.True(bitmap.Width <= 200, $"width exceeded the target: {bitmap.Width}");
        Assert.True(bitmap.Height <= 300, $"height exceeded the target: {bitmap.Height}");
    }

    [Fact]
    public void DecodingDoesNotEnlarge()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path, new ImageSize(600, 1024));

        Assert.Equal(80, bitmap.Width);
        Assert.Equal(120, bitmap.Height);
    }

    [Fact]
    public void EncodedAndOrientedSizesAreReadCorrectly()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        Assert.Equal(new ImageSize(80, 120), PageImagePipeline.ReadEncodedSize(jpeg.Path));
        Assert.Equal(new ImageSize(120, 80), PageImagePipeline.ReadOrientedSize(jpeg.Path));
    }

    #endregion

    #region Cropping

    [Fact]
    public void RightHalfCanBeCroppedOut()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var source = PageImagePipeline.DecodeOriented(jpeg.Path);
        using var cropped = PageImagePipeline.Crop(source, left: 40, top: 0, width: 40, height: 120);

        Assert.Equal(40, cropped.Width);
        Assert.Equal(120, cropped.Height);
        Assert.True(IsBlue(cropped.GetPixel(20, 60)), "cropped area should be entirely blue");
    }

    [Fact]
    public void CropUsesTheCoordinateSystemOfTheRotatedImage()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        // After rotation it is 120x80 with the upper half red. Crop the upper half.
        using var source = PageImagePipeline.DecodeOriented(jpeg.Path);
        using var cropped = PageImagePipeline.Crop(source, left: 0, top: 0, width: 120, height: 40);

        Assert.Equal(120, cropped.Width);
        Assert.Equal(40, cropped.Height);
        Assert.True(IsRed(cropped.GetPixel(60, 20)), "cropped area should be entirely red");
    }

    [Fact]
    public void CropOverflowingTheImageThrows()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var source = PageImagePipeline.DecodeOriented(jpeg.Path);

        Assert.Throws<ArgumentOutOfRangeException>(() => PageImagePipeline.Crop(source, 0, 0, 81, 120));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageImagePipeline.Crop(source, -1, 0, 40, 120));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageImagePipeline.Crop(source, 0, 0, 0, 120));
    }

    #endregion

    #region Corner dots

    [Fact]
    public void A2x2BlackDotIsDrawnAtEachCorner()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        PageImagePipeline.DrawCornerDots(bitmap);

        // The 2x2 block at each corner is black
        foreach (var (x, y) in new[] { (0, 0), (1, 1), (78, 0), (79, 1), (0, 118), (1, 119), (78, 118), (79, 119) })
        {
            Assert.Equal(SKColors.Black, bitmap.GetPixel(x, y));
        }
    }

    [Fact]
    public void CornerDotsDoNotReachTheThirdPixelFromTheCorner()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        PageImagePipeline.DrawCornerDots(bitmap);

        Assert.True(IsRed(bitmap.GetPixel(2, 2)), "inner area should keep the original color");
        Assert.True(IsRed(bitmap.GetPixel(30, 60)), "center should keep the original color");
    }

    [Fact]
    public void DrawingCornerDotsDoesNotChangeTheSize()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        PageImagePipeline.DrawCornerDots(bitmap);

        Assert.Equal(80, bitmap.Width);
        Assert.Equal(120, bitmap.Height);
    }

    [Fact]
    public void AllCornerDotsAreOpaqueBlack()
    {
        // The original hard-coded the channel order of the pixel array as BGRA.
        // Here everything is handled as BGRA8888, so colors cannot be mixed up.
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);

        PageImagePipeline.DrawCornerDots(bitmap);

        var corner = bitmap.GetPixel(0, 0);
        Assert.Equal(0, corner.Red);
        Assert.Equal(0, corner.Green);
        Assert.Equal(0, corner.Blue);
        Assert.Equal(255, corner.Alpha);
    }

    #endregion

    #region Encoding

    [Fact]
    public void JpegOutputCanBeReadBack()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);
        var outputPath = Path.Combine(Path.GetDirectoryName(jpeg.Path)!, "out.jpg");

        PageImagePipeline.Encode(bitmap, outputPath, OutputImageFormat.Jpeg, 90);

        Assert.True(File.Exists(outputPath));
        Assert.Equal(new ImageSize(80, 120), PageImagePipeline.ReadEncodedSize(outputPath));
    }

    [Fact]
    public void PngOutputFormatComesFromTheParameterNotTheExtension()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);
        var outputPath = Path.Combine(Path.GetDirectoryName(jpeg.Path)!, "out.png");

        PageImagePipeline.Encode(bitmap, outputPath, OutputImageFormat.Png, 90);

        var bytes = File.ReadAllBytes(outputPath);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);  // PNG signature
    }

    [Fact]
    public void HigherQualityProducesALargerFile()
    {
        using var jpeg = TempJpeg.Create(400, 600);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);
        var directory = Path.GetDirectoryName(jpeg.Path)!;
        var lowPath = Path.Combine(directory, "low.jpg");
        var highPath = Path.Combine(directory, "high.jpg");

        PageImagePipeline.Encode(bitmap, lowPath, OutputImageFormat.Jpeg, 30);
        PageImagePipeline.Encode(bitmap, highPath, OutputImageFormat.Jpeg, 95);

        Assert.True(
            new FileInfo(highPath).Length > new FileInfo(lowPath).Length,
            "the quality 95 file is not larger than the quality 30 one");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void InvalidJpegQualityThrows(int quality)
    {
        using var jpeg = TempJpeg.Create(80, 120);
        using var bitmap = PageImagePipeline.DecodeOriented(jpeg.Path);
        var outputPath = Path.Combine(Path.GetDirectoryName(jpeg.Path)!, "out.jpg");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PageImagePipeline.Encode(bitmap, outputPath, OutputImageFormat.Jpeg, quality));
    }

    [Theory]
    [InlineData("a.jpg", OutputImageFormat.Jpeg)]
    [InlineData("a.JPEG", OutputImageFormat.Jpeg)]
    [InlineData("a.png", OutputImageFormat.Png)]
    [InlineData("a.PNG", OutputImageFormat.Png)]
    public void OutputFormatIsDeterminedFromTheExtension(string filename, OutputImageFormat expected)
    {
        Assert.Equal(expected, PageImagePipeline.FormatFromExtension(filename));
    }

    [Fact]
    public void UnsupportedExtensionThrows()
    {
        Assert.Throws<InvalidDataException>(() => PageImagePipeline.FormatFromExtension("a.gif"));
    }

    #endregion
}
