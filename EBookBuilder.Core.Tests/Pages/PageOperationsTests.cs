using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;
using static Lpubsppop01.EBookBuilder.Core.Tests.TestSupport.TestImages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Pages;

/// <summary>Verification of page operations. Reordering is tracked by content hash.</summary>
public class PageOperationsTests
{
    #region Rotation

    [Fact]
    public void RotationRewritesTheExifTag()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        Assert.Equal(ExifOrientation.Rotate90CW, ExifOrientationStore.Read(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void RepeatedRotationsAccumulateTheOrientation()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);
        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        Assert.Equal(ExifOrientation.Rotate180, ExifOrientationStore.Read(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void RotationIsEffectiveOnScannedPages()
    {
        // Pages that already carry a big-endian EXIF block used to come back unrotated: the
        // orientation was written with the wrong EXIF type, which no reader accepts.
        using var folder = TempPageFolder.CreateWithScannedPage();

        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);
        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        var path = folder.FilePath("0.jpg");
        Assert.Equal(ExifOrientation.Rotate180, ExifOrientationStore.Read(path));
        Assert.Equal(SKEncodedOrigin.BottomRight, TestImages.ReadEncodedOrigin(path));
    }

    [Fact]
    public void NoRotationAmountChangesNothing()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");
        var before = TestImages.ContentHash(folder.FilePath("0.jpg"));

        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.None);

        Assert.Equal(before, TestImages.ContentHash(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void RotationDoesNotDegradePixels()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        // The file bytes change because of the added EXIF segment, so compare the decoded result.
        var before = TestImages.PixelHash(folder.FilePath("0.jpg"));

        for (var i = 0; i < 8; ++i)
        {
            PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);
        }

        // 8 rotations = 2 full turns. Only the tag changes, so the pixels should be unchanged.
        Assert.Equal(before, TestImages.PixelHash(folder.FilePath("0.jpg")));
        Assert.Equal(ExifOrientation.HorizontalNormal, ExifOrientationStore.Read(folder.FilePath("0.jpg")));
    }

    [Fact]
    public async Task AllPagesCanBeRotatedAtOnce()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(5);
        var filenames = folder.EnumerateFilenames();

        await PageOperations.RotateAllAsync(folder.Path, filenames, RotationAmount.Deg90);

        foreach (var filename in filenames)
        {
            Assert.Equal(ExifOrientation.Rotate90CW, ExifOrientationStore.Read(folder.FilePath(filename)));
        }
    }

    #endregion

    #region Cropping

    [Fact]
    public void ResultingSizeHasTheMarginsRemoved()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Crop(folder.Path, "0.jpg", left: 10, top: 20, right: 10, bottom: 20);

        Assert.Equal(new ImageSize(60, 80), PageImagePipeline.ReadOrientedSize(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void ZeroMarginsLeaveTheSizeUnchanged()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Crop(folder.Path, "0.jpg", 0, 0, 0, 0);

        Assert.Equal(new ImageSize(80, 120), PageImagePipeline.ReadOrientedSize(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void RotatedPageIsCroppedInOrientationAwareCoordinates()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");
        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        // After rotation it is 120x80 with the upper half red. Remove the bottom 40 pixels.
        PageOperations.Crop(folder.Path, "0.jpg", left: 0, top: 0, right: 0, bottom: 40);

        var path = folder.FilePath("0.jpg");
        Assert.Equal(new ImageSize(120, 40), PageImagePipeline.ReadOrientedSize(path));

        using var bitmap = PageImagePipeline.DecodeOriented(path);
        Assert.True(IsRed(bitmap.GetPixel(60, 20)), "remaining area should be entirely red");
    }

    [Fact]
    public void OrientationTagIsNotLeftBehindAfterCropping()
    {
        // The rotation is baked into the pixels, so leaving the tag would rotate it a second time.
        using var folder = TempPageFolder.CreateWithPages("0.jpg");
        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        PageOperations.Crop(folder.Path, "0.jpg", 0, 0, 0, 0);

        Assert.Equal(ExifOrientation.HorizontalNormal, ExifOrientationStore.Read(folder.FilePath("0.jpg")));
    }

    [Fact]
    public void ImageIsStillReadableAfterCropping()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Crop(folder.Path, "0.jpg", 10, 10, 10, 10);

        Assert.True(File.Exists(folder.FilePath("0.jpg")));
        Assert.Single(folder.AllFilenames());
    }

    [Fact]
    public void NoTemporaryFilesAreLeftBehind()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        PageOperations.Crop(folder.Path, "0.jpg", 10, 10, 10, 10);

        Assert.Equal(["0.jpg"], folder.AllFilenames());
    }

    [Fact]
    public void ExcessiveMarginsThrowAndDoNotDamageTheOriginalFile()
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");
        var before = TestImages.PixelHash(folder.FilePath("0.jpg"));

        Assert.Throws<InvalidOperationException>(
            () => PageOperations.Crop(folder.Path, "0.jpg", left: 50, top: 0, right: 50, bottom: 0));

        Assert.Equal(before, TestImages.PixelHash(folder.FilePath("0.jpg")));
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void NegativeMarginsThrow(int left, int top, int right, int bottom)
    {
        using var folder = TempPageFolder.CreateWithPages("0.jpg");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PageOperations.Crop(folder.Path, "0.jpg", left, top, right, bottom));
    }

    [Fact]
    public void AllPagesCanBeCroppedAtOnce()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();

        PageOperations.CropAll(folder.Path, filenames, left: 10, top: 20, right: 10, bottom: 20);

        foreach (var filename in filenames)
        {
            Assert.Equal(new ImageSize(60, 80), PageImagePipeline.ReadOrientedSize(folder.FilePath(filename)));
        }
    }

    [Fact]
    public void CropProgressIsReportedOncePerPage()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();
        var progress = new SyncProgress<PageProgress>();

        PageOperations.CropAll(folder.Path, filenames, 10, 20, 10, 20, progress: progress);

        Assert.Equal(3, progress.Reports.Count);
        Assert.Equal(100, progress.Reports[^1].Percentage);
    }

    [Fact]
    public void NoTemporaryFilesAreLeftBehindAfterCroppingSeveralPages()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();

        PageOperations.CropAll(folder.Path, filenames, 10, 20, 10, 20);

        // A leftover ".cropping" file would show up here
        Assert.Equal(filenames, folder.EnumerateFilenames());
    }

    #endregion

    #region Serial renaming

    [Fact]
    public void ScatteredNamesAreRenamedToSerialNumbers()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg", "c.jpg");
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.RenameWithSerialNumbers(folder.Path, filenames);

        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], result);
        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], folder.EnumerateFilenames());
    }

    [Fact]
    public void NameCollisionsLoseNoPages()
    {
        // We want to rename "2.jpg" to "1.jpg", but "1.jpg" is still in use.
        // As in the original, files are moved aside with a temp_ prefix before this is resolved.
        using var folder = TempPageFolder.CreateWithPages("1.jpg", "2.jpg");
        var hashesBefore = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.RenameWithSerialNumbers(folder.Path, filenames);

        Assert.Equal(["0.jpg", "1.jpg"], result);
        Assert.Equal(2, folder.AllFilenames().Count);
        Assert.DoesNotContain(folder.AllFilenames(), n => n.StartsWith("temp_", StringComparison.Ordinal));

        // The order of the contents should be unchanged
        Assert.Equal(hashesBefore, folder.ContentHashes());
    }

    [Fact]
    public void AlreadySerialNamesAreLeftUntouched()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(4);
        var hashesBefore = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.RenameWithSerialNumbers(folder.Path, filenames);

        Assert.Equal(filenames, result);
        Assert.Equal(hashesBefore, folder.ContentHashes());
    }

    [Fact]
    public void JpegExtensionIsNormalizedToJpg()
    {
        using var folder = TempPageFolder.CreateWithPages("a.jpeg", "b.jpeg");
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.RenameWithSerialNumbers(folder.Path, filenames);

        Assert.Equal(["0.jpg", "1.jpg"], result);
    }

    [Fact]
    public void RenameProgressIsReportedOncePerPage()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg", "c.jpg");
        var filenames = folder.EnumerateFilenames();
        var progress = new SyncProgress<PageProgress>();

        PageOperations.RenameWithSerialNumbers(folder.Path, filenames, progress);

        Assert.Equal(3, progress.Reports.Count);
        Assert.Equal(100, progress.Reports[^1].Percentage);
    }

    #endregion

    #region Duplication

    [Fact]
    public void DuplicateToNextIsInsertedAfterTheSourcePage()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3, makeDistinct: true);
        var before = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        PageOperations.Duplicate(folder.Path, filenames, index: 0, toLast: false);

        // The duplicate of the first page goes second, and the rest shift back by one
        Assert.Equal([before[0], before[0], before[1], before[2]], folder.ContentHashes());
    }

    [Fact]
    public void DuplicateToLastIsAppendedAtTheEnd()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3, makeDistinct: true);
        var before = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        PageOperations.Duplicate(folder.Path, filenames, index: 1, toLast: true);

        Assert.Equal([before[0], before[1], before[2], before[1]], folder.ContentHashes());
    }

    [Fact]
    public void NamesAreStillSerialAfterDuplication()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.Duplicate(folder.Path, filenames, index: 0, toLast: false);

        Assert.Equal(["0.jpg", "1.jpg", "2.jpg", "3.jpg"], result);
        Assert.Equal(4, folder.AllFilenames().Count);
    }

    #endregion

    #region Reordering and deletion

    [Fact]
    public void MovingToLastChangesTheOrder()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3, makeDistinct: true);
        var before = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        PageOperations.MoveToLast(folder.Path, filenames, index: 0);

        Assert.Equal([before[1], before[2], before[0]], folder.ContentHashes());
    }

    [Fact]
    public void NamesAreStillSerialAfterMovingToLast()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();

        var result = PageOperations.MoveToLast(folder.Path, filenames, index: 0);

        Assert.Equal(["0.jpg", "1.jpg", "2.jpg"], result);
    }

    [Fact]
    public void DeletingRemovesTheFile()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3, makeDistinct: true);
        var before = folder.ContentHashes();
        var filenames = folder.EnumerateFilenames();

        PageOperations.Delete(folder.Path, filenames[1]);

        Assert.False(File.Exists(folder.FilePath(filenames[1])));
        // Serial numbers are not closed up (same behavior as the original)
        Assert.Equal([before[0], before[2]], folder.ContentHashes());
        Assert.Equal(["0.jpg", "2.jpg"], folder.EnumerateFilenames());
    }

    #endregion
}
