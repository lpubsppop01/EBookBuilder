using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Exif;

/// <summary>
/// Verifies reading and writing EXIF tags with real files.
/// </summary>
/// <remarks>
/// These tests also serve to verify that ExifLibNet actually works on .NET 10 / Linux.
/// At the same time they confirm a property fundamental to this app:
/// rewriting tags does not degrade the pixels.
/// </remarks>
public class ExifOrientationStoreTests
{
    [Fact]
    public void JpegWithoutExifTagReadsAsHorizontalNormal()
    {
        using var jpeg = TempJpeg.Create();
        Assert.Equal(ExifOrientation.HorizontalNormal, ExifOrientationStore.Read(jpeg.Path));
    }

    [Theory]
    [InlineData(ExifOrientation.Rotate90CW)]
    [InlineData(ExifOrientation.Rotate180)]
    [InlineData(ExifOrientation.Rotate270CW)]
    [InlineData(ExifOrientation.HorizontalNormal)]
    public void WrittenOrientationReadsBackUnchanged(ExifOrientation orientation)
    {
        using var jpeg = TempJpeg.Create();
        ExifOrientationStore.Write(jpeg.Path, orientation);
        Assert.Equal(orientation, ExifOrientationStore.Read(jpeg.Path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RotationIsEffectiveOnFilesThatAlreadyHaveAnExifBlock(bool bigEndian)
    {
        // Pages that come from a camera or a scanner already carry an EXIF block, and its byte
        // order is often big-endian. Files this app gives a block to get a little-endian one,
        // which is why the round trip above can pass while rotation does nothing in the field.
        using var jpeg = TempJpeg.CreateWithExif(bigEndian);

        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        Assert.Equal(ExifOrientation.Rotate90CW, ExifOrientationStore.Read(jpeg.Path));
        Assert.Equal(SKEncodedOrigin.RightTop, TestImages.ReadEncodedOrigin(jpeg.Path));
    }

    [Fact]
    public void RotationsAccumulateOnFilesThatAlreadyHaveAnExifBlock()
    {
        using var jpeg = TempJpeg.CreateWithExif();

        var orientation = ExifOrientationStore.Read(jpeg.Path);
        for (var i = 0; i < 2; ++i)
        {
            orientation = orientation.Rotated90();
            ExifOrientationStore.Write(jpeg.Path, orientation);
        }

        Assert.Equal(ExifOrientation.Rotate180, ExifOrientationStore.Read(jpeg.Path));
        Assert.Equal(SKEncodedOrigin.BottomRight, TestImages.ReadEncodedOrigin(jpeg.Path));
    }

    [Fact]
    public void ConsecutiveRewritesLeaveTheLastValue()
    {
        using var jpeg = TempJpeg.Create();
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate180);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate270CW);
        Assert.Equal(ExifOrientation.Rotate270CW, ExifOrientationStore.Read(jpeg.Path));
    }

    [Fact]
    public void RewritingTagDoesNotChangePixels()
    {
        // This is fundamental to this app. Rewriting EXIF does not involve re-encoding the
        // JPEG, so the decoded pixels must not change by even a single byte.
        using var jpeg = TempJpeg.Create();
        var before = jpeg.ReadPixelBytes();

        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        Assert.Equal(before, jpeg.ReadPixelBytes());
    }

    [Fact]
    public void ImageIsStillReadableAfterRewritingTag()
    {
        using var jpeg = TempJpeg.Create(80, 120);
        ExifOrientationStore.Write(jpeg.Path, ExifOrientation.Rotate90CW);

        // The tag only specifies the orientation; the actual pixel dimensions do not change
        Assert.Equal((80, 120), jpeg.ReadSize());
    }

    [Fact]
    public void RepeatedRotationsDoNotDegradePixels()
    {
        using var jpeg = TempJpeg.Create();
        var before = jpeg.ReadPixelBytes();

        var orientation = ExifOrientation.HorizontalNormal;
        foreach (var _ in Enumerable.Range(0, 12))
        {
            orientation = orientation.Rotated90();
            ExifOrientationStore.Write(jpeg.Path, orientation);
        }

        // 12 rotations = 3 full turns, so the orientation should be back to the original
        Assert.Equal(ExifOrientation.HorizontalNormal, ExifOrientationStore.Read(jpeg.Path));
        Assert.Equal(before, jpeg.ReadPixelBytes());
    }

    [Fact]
    public void ReadingMissingFileThrows()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.jpg");
        Assert.ThrowsAny<Exception>(() => ExifOrientationStore.Read(missing));
    }
}
