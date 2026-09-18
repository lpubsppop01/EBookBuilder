using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;
using static Lpubsppop01.EBookBuilder.Core.Tests.TestSupport.TestImages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build;

/// <summary>Verification of PDF export.</summary>
public class PdfBuilderTests
{
    static string OutputPathFor(TempPageFolder folder, string name = "output.pdf") => folder.FilePath(name);

    static BuildOptions OptionsFor(TempPageFolder folder, Func<BuildOptions, BuildOptions>? configure = null)
    {
        var options = new BuildOptions
        {
            OutputFilePath = OutputPathFor(folder),
            ContainerKind = BuildContainerKind.Pdf,
        };
        return configure is null ? options : configure(options);
    }

    static async Task<(BuildResult Result, PdfInspector Pdf)> BuildAsync(
        TempPageFolder folder, BuildOptions options, IProgress<PageProgress>? progress = null)
    {
        var filenames = folder.EnumerateFilenames();
        var result = await PdfBuilder.BuildAsync(folder.Path, filenames, options, progress);
        return (result, PdfInspector.Load(options.OutputFilePath));
    }

    static (int Width, int Height) ReadPayloadSize(byte[] payload)
    {
        using var bitmap = SKBitmap.Decode(payload);
        return (bitmap.Width, bitmap.Height);
    }

    static SKColor ReadPayloadPixel(byte[] payload, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(payload);
        return bitmap.GetPixel(x, y);
    }

    #region Lossless embedding

    /// <summary>
    /// The property the whole design rests on: each page goes into the PDF as the very bytes of the
    /// file it came from, in page order, with no re-encoding.
    /// </summary>
    [Fact]
    public async Task PagesAreEmbeddedVerbatimAsDctDecode()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(4, makeDistinct: true);
        var (result, pdf) = await BuildAsync(folder, OptionsFor(folder));

        var images = pdf.Images;
        Assert.Equal(4, images.Count);
        foreach (var image in images)
            Assert.Contains("/Filter /DCTDecode", image.Dictionary, StringComparison.Ordinal);

        for (var i = 0; i < images.Count; ++i)
            Assert.Equal(File.ReadAllBytes(folder.FilePath(PageNaming.FormatFilename(i, 4))), images[i].Payload);

        Assert.Equal(4, result.CopiedCount);
        Assert.Equal(0, result.ReEncodedCount);
    }

    /// <summary>
    /// The flagship guarantee: rotating a page costs no image quality, because the rotation is
    /// written as the page placement and the JPEG bytes are carried across untouched.
    /// </summary>
    [Fact]
    public async Task RotatedPageKeepsItsBytesAndRotatesByTransform()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2, width: 80, height: 120);
        PageOperations.Rotate(folder.Path, "0.jpg", RotationAmount.Deg90);

        var (result, pdf) = await BuildAsync(folder, OptionsFor(folder));

        Assert.Equal(ExifOrientation.Rotate90CW, ExifOrientationStore.Read(folder.FilePath("0.jpg")));

        // The bytes are still the source file's own, rotation included.
        Assert.Equal(File.ReadAllBytes(folder.FilePath("0.jpg")), pdf.Images[0].Payload);
        Assert.Equal(File.ReadAllBytes(folder.FilePath("1.jpg")), pdf.Images[1].Payload);
        Assert.Equal(2, result.CopiedCount);

        // 80x120 at 300 dpi is 19.2x28.8 points; the quarter turn turns the page box on its side.
        var pages = pdf.Pages;
        Assert.Contains("/MediaBox [ 0 0 28.8 19.2 ]", pages[0], StringComparison.Ordinal);
        Assert.Contains("/MediaBox [ 0 0 19.2 28.8 ]", pages[1], StringComparison.Ordinal);

        Assert.Equal("q 0 -19.2 28.8 0 0 19.2 cm /Im0 Do Q", pdf.ContentStreams[0]);
        Assert.Equal("q 19.2 0 0 28.8 0 0 cm /Im0 Do Q", pdf.ContentStreams[1]);
    }

    /// <summary>A page turned half way round keeps its bytes as well.</summary>
    [Theory]
    [InlineData(RotationAmount.Deg90, "q 0 -19.2 28.8 0 0 19.2 cm /Im0 Do Q")]
    [InlineData(RotationAmount.Deg180, "q -19.2 0 0 -28.8 19.2 28.8 cm /Im0 Do Q")]
    [InlineData(RotationAmount.Deg270, "q 0 19.2 -28.8 0 28.8 0 cm /Im0 Do Q")]
    public async Task EveryRotationIsWrittenAsAPlacement(RotationAmount amount, string expectedContentStream)
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        PageOperations.Rotate(folder.Path, "0.jpg", amount);

        var (_, pdf) = await BuildAsync(folder, OptionsFor(folder));

        Assert.Equal(File.ReadAllBytes(folder.FilePath("0.jpg")), pdf.Images[0].Payload);
        Assert.Equal(expectedContentStream, pdf.ContentStreams[0]);
    }

    #endregion

    #region Re-encoding

    /// <summary>
    /// When the page has to be resized it is re-encoded, but it still enters the PDF as a JPEG
    /// rather than as raw samples, so the file stays small.
    /// </summary>
    [Fact]
    public async Task SpecifiedSizeReEncodesAndEmbedsJpeg()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2, width: 80, height: 120);
        var options = OptionsFor(folder, o => o with
        {
            SizeKind = BuildSizeKind.Specified,
            TargetSize = new ImageSize(40, 60),
        });

        var (result, pdf) = await BuildAsync(folder, options);

        Assert.Equal(2, result.ReEncodedCount);
        Assert.Equal(0, result.CopiedCount);

        foreach (var image in pdf.Images)
        {
            Assert.Contains("/Filter /DCTDecode", image.Dictionary, StringComparison.Ordinal);
            Assert.Equal((40, 60), ReadPayloadSize(image.Payload!));
        }

        Assert.NotEqual(File.ReadAllBytes(folder.FilePath("0.jpg")), pdf.Images[0].Payload);

        // The page box follows the resized image, with no margin added around it.
        Assert.Contains("/MediaBox [ 0 0 9.6 14.4 ]", pdf.Pages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CornerDotsAreDrawn()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var options = OptionsFor(folder, o => o with { DrawsCornerDots = true });

        var (_, pdf) = await BuildAsync(folder, options);
        var payload = pdf.Images[0].Payload!;

        Assert.True(IsDark(ReadPayloadPixel(payload, 0, 0)));
        Assert.True(IsDark(ReadPayloadPixel(payload, 79, 119)));
    }

    /// <summary>
    /// A grayscale page stores one component, so it has to be declared as DeviceGray. Declaring it
    /// as DeviceRGB instead shows the page in the wrong colours.
    /// </summary>
    [Fact]
    public async Task GrayPagesUseDeviceGray()
    {
        using var folder = TempPageFolder.CreateEmpty();
        WriteGrayJpeg(folder.FilePath("0.jpg"), 80, 120);

        var (result, pdf) = await BuildAsync(folder, OptionsFor(folder));

        Assert.Contains("/ColorSpace /DeviceGray", pdf.Images[0].Dictionary, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllBytes(folder.FilePath("0.jpg")), pdf.Images[0].Payload);
        Assert.Equal(1, result.CopiedCount);
    }

    [Fact]
    public async Task ColorPagesUseDeviceRGB()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120);
        var (_, pdf) = await BuildAsync(folder, OptionsFor(folder));

        Assert.Contains("/ColorSpace /DeviceRGB", pdf.Images[0].Dictionary, StringComparison.Ordinal);
    }

    #endregion

    #region Document shape

    /// <summary>
    /// Unlike the CBZ, which stamps the current time into every ZIP entry, the PDF has no timestamp
    /// in it, so the same pages always produce the same file.
    /// </summary>
    [Fact]
    public async Task TheSamePagesProduceTheSameFile()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3, makeDistinct: true);

        var first = OptionsFor(folder, o => o with { OutputFilePath = OutputPathFor(folder, "first.pdf") });
        await BuildAsync(folder, first);

        var second = OptionsFor(folder, o => o with { OutputFilePath = OutputPathFor(folder, "second.pdf") });
        await BuildAsync(folder, second);

        Assert.Equal(
            File.ReadAllBytes(first.OutputFilePath),
            File.ReadAllBytes(second.OutputFilePath));
    }

    [Fact]
    public async Task ThePageCountAndKidsMatchThePages()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var (_, pdf) = await BuildAsync(folder, OptionsFor(folder));

        Assert.Equal(3, pdf.Pages.Count);
        Assert.Contains("/Count 3", pdf.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every object has to be one well-formed dictionary.
    /// </summary>
    /// <remarks>
    /// This is checked by counting brackets rather than by looking for entries, because a doubled
    /// opening bracket still contains every entry a substring assertion would look for. Asking
    /// poppler is not enough either: it repairs the file and reports success, so a malformed
    /// document can pass an external check that only looks at the exit code.
    /// </remarks>
    [Fact]
    public async Task EveryObjectIsAWellFormedDictionary()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var (_, pdf) = await BuildAsync(folder, OptionsFor(folder));

        foreach (var item in pdf.Objects) AssertBalancedDictionary(item.Dictionary);
    }

    static void AssertBalancedDictionary(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; ++i)
        {
            if (text[i] == '<' && i + 1 < text.Length && text[i + 1] == '<')
            {
                ++depth;
                ++i;
                continue;
            }

            if (text[i] == '>' && i + 1 < text.Length && text[i + 1] == '>')
            {
                --depth;
                Assert.True(depth >= 0, $"A dictionary closes more than it opens: {text}");
                ++i;
                continue;
            }

            if (text[i] is '<' or '>')
                Assert.Fail($"A lone angle bracket at {i}: {text}");
        }

        Assert.Equal(0, depth);
    }

    /// <summary>
    /// The cross-reference table is read by multiplying a fixed record size, so every entry has to
    /// be exactly as long as the format says.
    /// </summary>
    [Fact]
    public async Task TheCrossReferenceTableIsWellFormed()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var (_, pdf) = await BuildAsync(folder, OptionsFor(folder));

        // Reading the entries already asserts their exact shape.
        Assert.Equal(pdf.Objects.Count, pdf.XrefOffsets.Count);
        Assert.Equal(pdf.Objects.Count + 1, int.Parse(
            System.Text.RegularExpressions.Regex.Match(pdf.Text, @"trailer\n<< /Size (\d+)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task AnExistingOutputFileIsOverwritten()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(2);
        var options = OptionsFor(folder);
        File.WriteAllText(options.OutputFilePath, "not a pdf");

        await BuildAsync(folder, options);

        var pdf = PdfInspector.Load(options.OutputFilePath);
        Assert.Equal(2, pdf.Images.Count);
    }

    #endregion

    #region Progress and preconditions

    [Fact]
    public async Task ProgressIsReportedOncePerPage()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(3);
        var progress = new SyncProgress<PageProgress>();

        await BuildAsync(folder, OptionsFor(folder), progress);

        Assert.Equal(3, progress.Reports.Count);
        Assert.Equal(100, progress.Reports[^1].Percentage);
    }

    [Fact]
    public async Task NonSerialNamesThrow()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg", "c.jpg");
        var filenames = folder.EnumerateFilenames();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PdfBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder)));
    }

    [Fact]
    public async Task NonSerialNamesDoNotCreateOutputFile()
    {
        using var folder = TempPageFolder.CreateWithPages("b.jpg", "a.jpg");
        var filenames = folder.EnumerateFilenames();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PdfBuilder.BuildAsync(folder.Path, filenames, OptionsFor(folder)));

        Assert.False(File.Exists(OutputPathFor(folder)));
    }

    #endregion

    #region Settings

    /// <summary>PDF has no PNG image type, so the combination is rejected rather than quietly changed.</summary>
    [Fact]
    public void PdfRejectsPng()
    {
        var options = new BuildOptions
        {
            OutputFilePath = "out.pdf",
            ContainerKind = BuildContainerKind.Pdf,
            ImageFormatKind = BuildImageFormatKind.Png,
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    /// <summary>
    /// System.Text.Json turns an out-of-range number into an undefined enum value without
    /// complaining, so a settings file could carry one and the dispatch would fall through to CBZ.
    /// </summary>
    [Fact]
    public void AnUndefinedContainerIsRejected()
    {
        var options = new BuildOptions
        {
            OutputFilePath = "out.pdf",
            ContainerKind = (BuildContainerKind)99,
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void AnEmptyOutputPathIsRejected()
    {
        var options = new BuildOptions { OutputFilePath = "   " };
        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(2401)]
    public void AnOutOfRangePageResolutionIsRejected(int dpi)
    {
        var options = new BuildOptions { OutputFilePath = "out.pdf", PdfPageDpi = dpi };
        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    #endregion

    #region Dispatch

    [Fact]
    public async Task TheContainerChoosesTheWriter()
    {
        using var folder = TempPageFolder.CreateWithSerialPages(1);

        var asPdf = OptionsFor(folder);
        await BookBuilder.BuildAsync(folder.Path, folder.EnumerateFilenames(), asPdf);
        Assert.StartsWith("%PDF-", ReadPrefix(asPdf.OutputFilePath, 5));

        var asCbz = new BuildOptions { OutputFilePath = OutputPathFor(folder, "output.cbz") };
        await BookBuilder.BuildAsync(folder.Path, folder.EnumerateFilenames(), asCbz);
        Assert.StartsWith("PK", ReadPrefix(asCbz.OutputFilePath, 2));
    }

    static string ReadPrefix(string path, int length) =>
        System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, length);

    #endregion
}
