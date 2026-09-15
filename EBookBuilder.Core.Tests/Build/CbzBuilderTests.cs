using System.IO.Compression;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;
using static Lpubsppop01.EBookBuilder.Core.Tests.TestSupport.TestImages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build;

/// <summary>Verification of CBZ export.</summary>
public class CbzBuilderTests
{
    static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    static (int Width, int Height) ReadEntrySize(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var bitmap = SKBitmap.Decode(stream);
        return (bitmap.Width, bitmap.Height);
    }

    static SKColor ReadEntryPixel(ZipArchiveEntry entry, int x, int y)
    {
        using var stream = entry.Open();
        using var bitmap = SKBitmap.Decode(stream);
        return bitmap.GetPixel(x, y);
    }

    static string OutputPathFor(TempPageFolder folder) => folder.FilePath("output.cbz");

    static BuildOptions OptionsFor(TempPageFolder folder, Func<BuildOptions, BuildOptions>? configure = null)
    {
        var options = new BuildOptions { OutputFilePath = OutputPathFor(folder) };
        return configure is null ? options : configure(options);
    }

    #region Lossless copy

    [Fact]
    public async Task OriginalSizeCopiesBytesExactlyWithoutReEncoding()
    {
        // The crux of this port. The original WPF version always re-encoded even for the
        // original size, so image quality degraded on every build.
        // Here we verify that the bytes match exactly.
        using var folder = TempPageFolder.CreateWithSerialPages(4, makeDistinct: true);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        Assert.Equal(4, result.PageCount);
        Assert.Equal(4, result.CopiedCount);
        Assert.Equal(0, result.ReEncodedCount);

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal(filenames.OrderBy(n => n, StringComparer.Ordinal), zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal));

        foreach (var entry in zip.Entries)
        {
            var sourceBytes = File.ReadAllBytes(folder.FilePath(entry.FullName));
            Assert.Equal(sourceBytes, ReadEntryBytes(entry));
        }
    }

    [Fact]
    public async Task OriginalSizeKeepsExifOrientationTag()
    {
        // The copy path does not rotate the pixels, so the orientation tag is the only
        // record of the rotation.
        using var folder = TempPageFolder.CreateWithSerialPages(1);
        var filenames = folder.EnumerateFilenames();
        Core.Pages.PageOperations.Rotate(folder.Path, filenames[0], Core.Exif.RotationAmount.Deg90);

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        var entry = zip.Entries.Single();

        // Reading EXIF requires a real file, so extract it into the folder (the folder is cleaned up as a whole).
        var extractedPath = folder.FilePath("extracted.jpg");
        entry.ExtractToFile(extractedPath, overwrite: true);

        Assert.Equal(Core.Exif.ExifOrientation.Rotate90CW, Core.Exif.ExifOrientationStore.Read(extractedPath));
    }

    #endregion

    #region Downscaling

    [Fact]
    public async Task SpecifiedSizeDownscalesKeepingAspectRatio()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2, width: 80, height: 120);
        var filenames = folder.EnumerateFilenames();

        // Fit 80x120 (ratio 0.667) into 40x60 -> exactly half
        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with
            {
                SizeKind = BuildSizeKind.Specified,
                TargetSize = new ImageSize(40, 60),
            }));

        Assert.Equal(2, result.ReEncodedCount);

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        foreach (var entry in zip.Entries)
        {
            Assert.Equal((40, 60), ReadEntrySize(entry));
        }
    }

    [Fact]
    public async Task SpecifiedSizeDoesNotPadWithMargins()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var filenames = folder.EnumerateFilenames();

        // The target box 40x100 does not match the original aspect ratio.
        // An implementation that pads with margins would produce 40x100, but here the fitted 40x60 is expected.
        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with
            {
                SizeKind = BuildSizeKind.Specified,
                TargetSize = new ImageSize(40, 100),
            }));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal((40, 60), ReadEntrySize(zip.Entries.Single()));
    }

    [Fact]
    public async Task SpecifiedSizeDoesNotEnlarge()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with
            {
                SizeKind = BuildSizeKind.Specified,
                TargetSize = new ImageSize(600, 1024),
            }));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal((80, 120), ReadEntrySize(zip.Entries.Single()));
    }

    [Fact]
    public async Task DownscalingBakesExifOrientationIntoPixels()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var filenames = folder.EnumerateFilenames();
        Core.Pages.PageOperations.Rotate(folder.Path, filenames[0], Core.Exif.RotationAmount.Deg90);

        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with
            {
                SizeKind = BuildSizeKind.Specified,
                TargetSize = new ImageSize(60, 40),
            }));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        var entry = zip.Entries.Single();

        // After rotation it is 120x80. Fitting into 60x40 gives 60x40.
        Assert.Equal((60, 40), ReadEntrySize(entry));

        // Rotating 90 degrees clockwise brings the original left edge (red) to the top edge
        Assert.True(IsRed(ReadEntryPixel(entry, 30, 5)), "upper half should be red");
        Assert.True(IsBlue(ReadEntryPixel(entry, 30, 35)), "lower half should be blue");
    }

    #endregion

    #region Corner dots

    [Fact]
    public async Task DrawingCornerDotsReEncodesEvenOnCopyPath()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with { DrawsCornerDots = true }));

        Assert.Equal(0, result.CopiedCount);
        Assert.Equal(2, result.ReEncodedCount);

        // JPEG is lossy, so the colors of neighboring pixels bleed slightly into the corner dots.
        // They are markers for checking the crop position by eye, so being sufficiently dark is enough.
        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        foreach (var entry in zip.Entries)
        {
            Assert.True(IsDark(ReadEntryPixel(entry, 0, 0)), "top-left is not dark");
            Assert.True(IsDark(ReadEntryPixel(entry, 79, 119)), "bottom-right is not dark");
            // x=40 is exactly the boundary between red and blue, so check well inside it
            Assert.True(IsRed(ReadEntryPixel(entry, 20, 60)), "center is not the original color");
        }
    }

    [Fact]
    public async Task CornerDotsAreDrawnAtOriginalSize()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with { DrawsCornerDots = true }));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal((80, 120), ReadEntrySize(zip.Entries.Single()));
    }

    #endregion

    #region Format and archive structure

    [Fact]
    public async Task PngOutputUsesPngExtension()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(
            folder.Path, filenames, OptionsFor(folder, o => o with { ImageFormatKind = BuildImageFormatKind.Png }));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal(["0.png", "1.png"], zip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ImagesArePlacedDirectlyUnderTheArchiveRoot()
    {
        // CBZ readers read images directly under the root as pages.
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.All(zip.Entries, entry => Assert.DoesNotContain('/', entry.FullName));
    }

    [Fact]
    public async Task EntriesAreOrderedByPageOrder()
    {
        // Some CBZ readers read pages in archive order, so the order must be deterministic.
        // Building the archive by scanning the directory would depend on the file system's return order.
        using var folder = TempPageFolder.CreateWithSerialPages(5);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal(filenames, zip.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public async Task EntriesAreStoredUncompressed()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var filenames = folder.EnumerateFilenames();

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.All(zip.Entries, entry =>
            Assert.Equal(entry.Length, entry.CompressedLength));
    }

    [Fact]
    public async Task ExistingOutputFileIsOverwritten()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var filenames = folder.EnumerateFilenames();
        var outputPath = OutputPathFor(folder);
        await File.WriteAllTextAsync(outputPath, "stale content");

        var result = await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder));

        using var zip = ZipFile.OpenRead(result.OutputFilePath);
        Assert.Equal(2, zip.Entries.Count);
    }

    [Fact]
    public async Task ProgressIsReportedOncePerPage()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var filenames = folder.EnumerateFilenames();
        var progress = new SyncProgress<Core.PageProgress>();

        await CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder), progress);

        Assert.Equal(3, progress.Reports.Count);
        Assert.Equal(100, progress.Reports[^1].Percentage);
    }

    #endregion

    #region Preconditions

    [Fact]
    public async Task NonSerialNamesThrow()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg", "c.jpg");
        var filenames = folder.EnumerateFilenames();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder)));
    }

    [Fact]
    public async Task NonSerialNamesDoNotCreateOutputFile()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg");
        var filenames = folder.EnumerateFilenames();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CbzBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder)));

        Assert.False(File.Exists(OutputPathFor(folder)));
    }

    #endregion
}
