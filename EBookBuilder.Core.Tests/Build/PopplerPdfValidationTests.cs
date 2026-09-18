using System.Diagnostics;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Pages;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;
using SkiaSharp;
using static Lpubsppop01.EBookBuilder.Core.Tests.TestSupport.TestImages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build;

/// <summary>
/// Checks the written PDF against independent readers rather than against our own parser.
/// </summary>
/// <remarks>
/// <para>
/// Everything else in this suite reads the file back with code written alongside the writer, which
/// can only confirm that the two agree. These tests hand the file to poppler and Ghostscript, which
/// share no code with us, and let them say whether it is a PDF at all and whether the pages come
/// out the right way round.
/// </para>
/// <para>
/// They need those tools installed, and xunit 2 does not offer a runtime skip, so each test returns
/// without asserting when its tool is missing. Run the rest with
/// <c>dotnet test --filter "Category!=RequiresExternalTools"</c>.
/// </para>
/// </remarks>
[Trait("Category", "RequiresExternalTools")]
public class PopplerPdfValidationTests
{
    static string? FindTool(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);

    static (int ExitCode, string StdOut, string StdErr) Run(string tool, params string[] args)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdOut, stdErr);
    }

    /// <summary>Builds a one page PDF whose page is red on the left and blue on the right.</summary>
    static async Task<(TempPageFolder Folder, string PdfPath)> BuildTwoToneAsync(
        RotationAmount rotation = RotationAmount.None, bool makeDistinct = false)
    {
        var folder = TempPageFolder.CreateWithSerialPages(1, width: 80, height: 120, makeDistinct: makeDistinct);
        if (rotation != RotationAmount.None)
            PageOperations.Rotate(folder.Path, "0.jpg", rotation);

        var options = new BuildOptions
        {
            OutputFilePath = folder.FilePath("output.pdf"),
            ContainerKind = BuildContainerKind.Pdf,
        };
        await PdfBuilder.BuildAsync(folder.Path, folder.EnumerateFilenames(), options);

        return (folder, options.OutputFilePath);
    }

    /// <summary>Rasterises the first page and decodes it, at one pixel per source pixel.</summary>
    static SKBitmap RenderPage(string pdfPath, string workDirectoryPath)
    {
        var prefix = Path.Combine(workDirectoryPath, "page");
        var (exitCode, _, stdErr) = Run(FindTool("pdftoppm")!, "-r", "300", "-png", "-singlefile", "-f", "1", "-l", "1", pdfPath, prefix);
        Assert.True(exitCode == 0, $"pdftoppm failed: {stdErr}");

        return SKBitmap.Decode(prefix + ".png");
    }

    #region Structure

    /// <summary>
    /// Ghostscript is the strictest common parser, so its silence is real evidence that the
    /// cross-reference table and the dictionaries are well formed.
    /// </summary>
    /// <remarks>
    /// The exit code alone is not enough. Ghostscript repairs what it can and still succeeds, so a
    /// malformed file passes an exit code check while printing a complaint; anything on the error
    /// stream is treated as a failure here.
    /// </remarks>
    [Fact]
    public async Task GhostscriptParsesTheOutputWithoutComplaint()
    {
        if (FindTool("gs") is not { } gs) return;

        var (folder, pdfPath) = await BuildTwoToneAsync();
        using var _ = folder;

        var (exitCode, _, stdErr) = Run(gs, "-dNOPAUSE", "-dBATCH", "-sDEVICE=nullpage", pdfPath);
        Assert.True(exitCode == 0, $"Ghostscript rejected the file: {stdErr}");
        Assert.True(stdErr.Trim().Length == 0, $"Ghostscript complained about the file: {stdErr}");
    }

    [Fact]
    public async Task PdfinfoReportsThePagesAndTheirPhysicalSizeWithoutComplaint()
    {
        if (FindTool("pdfinfo") is not { } pdfinfo) return;

        var (folder, pdfPath) = await BuildTwoToneAsync();
        using var _ = folder;

        var (exitCode, stdOut, stdErr) = Run(pdfinfo, pdfPath);
        Assert.True(exitCode == 0, $"pdfinfo rejected the file: {stdErr}");
        Assert.True(stdErr.Trim().Length == 0, $"pdfinfo complained about the file: {stdErr}");

        Assert.Contains("Pages:           1", stdOut, StringComparison.Ordinal);
        // 80x120 pixels at the default 300 dpi is 19.2x28.8 points.
        Assert.Contains("Page size:       19.2 x 28.8 pts", stdOut, StringComparison.Ordinal);
    }

    #endregion

    #region Losslessness

    /// <summary>
    /// The strongest available proof of passthrough: pdfimages lifts the stored stream out whole,
    /// and it matches the source file byte for byte.
    /// </summary>
    [Fact]
    public async Task TheStoredImageIsTheSourceFileByteForByte()
    {
        if (FindTool("pdfimages") is not { } pdfimages) return;

        var (folder, pdfPath) = await BuildTwoToneAsync();
        using var _ = folder;

        var extractedDirectoryPath = Directory.CreateDirectory(Path.Combine(folder.Path, "extracted")).FullName;
        var (exitCode, _, stdErr) = Run(pdfimages, "-j", pdfPath, Path.Combine(extractedDirectoryPath, "image"));
        Assert.True(exitCode == 0, $"pdfimages failed: {stdErr}");

        var extracted = Directory.GetFiles(extractedDirectoryPath).OrderBy(p => p).ToArray();
        Assert.Single(extracted);
        Assert.Equal(File.ReadAllBytes(folder.FilePath("0.jpg")), File.ReadAllBytes(extracted[0]));
    }

    /// <summary>A rotated page is stored the same way, with the rotation living in the placement.</summary>
    [Fact]
    public async Task ARotatedPageIsAlsoStoredByteForByte()
    {
        if (FindTool("pdfimages") is not { } pdfimages) return;

        var (folder, pdfPath) = await BuildTwoToneAsync(RotationAmount.Deg90);
        using var _ = folder;

        var extractedDirectoryPath = Directory.CreateDirectory(Path.Combine(folder.Path, "extracted")).FullName;
        var (exitCode, _, stdErr) = Run(pdfimages, "-j", pdfPath, Path.Combine(extractedDirectoryPath, "image"));
        Assert.True(exitCode == 0, $"pdfimages failed: {stdErr}");

        var extracted = Directory.GetFiles(extractedDirectoryPath).OrderBy(p => p).ToArray();
        Assert.Single(extracted);
        Assert.Equal(File.ReadAllBytes(folder.FilePath("0.jpg")), File.ReadAllBytes(extracted[0]));
    }

    #endregion

    #region Rendering

    /// <summary>
    /// The orientation, the page box, the placement matrix and the colour space all have to be
    /// right for the page to come out the way it is supposed to, and this checks all of them at
    /// once against a renderer that knows nothing about how they were written.
    /// </summary>
    [Fact]
    public async Task AnUnrotatedPageRendersWithItsOriginalColours()
    {
        if (FindTool("pdftoppm") is null) return;

        var (folder, pdfPath) = await BuildTwoToneAsync();
        using var _ = folder;

        using var rendered = RenderPage(pdfPath, folder.Path);

        Assert.Equal(80, rendered.Width);
        Assert.Equal(120, rendered.Height);
        Assert.True(IsRed(rendered.GetPixel(15, 60)), "The left half should have stayed red.");
        Assert.True(IsBlue(rendered.GetPixel(65, 60)), "The right half should have stayed blue.");
    }

    /// <summary>
    /// Rotating a quarter turn clockwise takes the left edge to the top, so the page should come
    /// out landscape and read red above blue.
    /// </summary>
    [Fact]
    public async Task ARotatedPageRendersRotated()
    {
        if (FindTool("pdftoppm") is null) return;

        var (folder, pdfPath) = await BuildTwoToneAsync(RotationAmount.Deg90);
        using var _ = folder;

        using var rendered = RenderPage(pdfPath, folder.Path);

        Assert.Equal(120, rendered.Width);
        Assert.Equal(80, rendered.Height);
        Assert.True(IsRed(rendered.GetPixel(60, 15)), "The top half should be the original left, which is red.");
        Assert.True(IsBlue(rendered.GetPixel(60, 65)), "The bottom half should be the original right, which is blue.");
    }

    [Fact]
    public async Task ARotatedHalfWayRoundPageRendersUpsideDown()
    {
        if (FindTool("pdftoppm") is null) return;

        var (folder, pdfPath) = await BuildTwoToneAsync(RotationAmount.Deg180);
        using var _ = folder;

        using var rendered = RenderPage(pdfPath, folder.Path);

        Assert.Equal(80, rendered.Width);
        Assert.Equal(120, rendered.Height);
        // Half a turn puts the original right on the left.
        Assert.True(IsBlue(rendered.GetPixel(15, 60)), "The left half should now be the original right, which is blue.");
        Assert.True(IsRed(rendered.GetPixel(65, 60)), "The right half should now be the original left, which is red.");
    }

    #endregion
}
