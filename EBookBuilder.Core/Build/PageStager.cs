using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>One page rendered into the temporary directory.</summary>
/// <param name="Name">The filename inside the temporary directory.</param>
/// <param name="Copied">Whether the bytes are identical to the source page, that is, whether it was not re-encoded.</param>
public readonly record struct StagedPage(string Name, bool Copied);

/// <summary>
/// Renders the pages into a temporary directory in parallel, for a container writer to consume
/// afterwards in page order.
/// </summary>
/// <remarks>
/// <para>
/// Both containers stage the pages the same way and differ only in how they finally package them,
/// so the rendering lives here rather than in either builder.
/// </para>
/// <para>
/// Rendering is parallel but packaging is not: a ZIP archive and a PDF are both written as one
/// sequential pass. Splitting the two keeps the expensive half (decoding and encoding images)
/// parallel while the cheap half stays ordered.
/// </para>
/// </remarks>
public sealed class PageStager : IDisposable
{
    /// <summary>Parent of the temporary working directories.</summary>
    static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "lpubsppop01.EBookBuilder");

    PageStager(string directoryPath, IReadOnlyList<StagedPage> pages, int copiedCount)
    {
        DirectoryPath = directoryPath;
        Pages = pages;
        CopiedCount = copiedCount;
    }

    /// <summary>The temporary directory the pages were rendered into.</summary>
    public string DirectoryPath { get; }

    /// <summary>The staged pages, in page order.</summary>
    public IReadOnlyList<StagedPage> Pages { get; }

    /// <summary>How many pages were stored without re-encoding.</summary>
    public int CopiedCount { get; }

    /// <summary>Renders every page into a fresh temporary directory.</summary>
    /// <param name="directoryPath">The folder holding the source pages.</param>
    /// <param name="filenames">The page filenames, in page order.</param>
    /// <param name="options">The build settings.</param>
    /// <param name="canUseVerbatim">
    /// Whether the source file's bytes may be stored as they are, decided per file. When omitted,
    /// every page that <see cref="BuildOptions.CanCopyWithoutReEncoding"/> allows is copied.
    /// </param>
    /// <param name="progress">Receives the page progress.</param>
    /// <param name="cancellationToken">Cancels the rendering.</param>
    /// <exception cref="InvalidOperationException">When the filenames are not serial numbers.</exception>
    public static async Task<PageStager> CreateAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        BuildOptions options,
        Func<string, bool>? canUseVerbatim = null,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Checked before the temporary directory is created, so a rejected build leaves nothing behind.
        if (!PageNaming.AreSerialNumbers(filenames))
            throw new InvalidOperationException("The filenames are not serial numbers. Rename them to serial numbers first.");

        var tempDirPath = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirPath);

        try
        {
            var done = 0;
            var copiedCount = 0;
            var countLock = new object();

            // The output names are determined by the page-order position, so fix them up front.
            var outputNames = filenames.Select(name => OutputNameFor(name, options)).ToArray();
            var copiedFlags = new bool[filenames.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, filenames.Count),
                cancellationToken,
                (index, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var copied = RenderPage(
                        directoryPath, tempDirPath, filenames[index], outputNames[index], options, canUseVerbatim);

                    lock (countLock)
                    {
                        copiedFlags[index] = copied;
                        if (copied) ++copiedCount;
                        progress?.Report(new PageProgress(++done, filenames.Count));
                    }

                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);

            var pages = outputNames
                .Select((name, index) => new StagedPage(name, copiedFlags[index]))
                .ToArray();

            return new PageStager(tempDirPath, pages, copiedCount);
        }
        catch
        {
            // Nothing is going to consume the directory, so do not leave it behind.
            TryDeleteDirectory(tempDirPath);
            throw;
        }
    }

    /// <summary>Returns the full path of a staged page.</summary>
    public string PathOf(string pageName) => Path.Combine(DirectoryPath, pageName);

    /// <summary>Deletes the temporary directory. Failure does not affect the result of the main work.</summary>
    public void Dispose() => TryDeleteDirectory(DirectoryPath);

    /// <summary>Determines the output filename for a page.</summary>
    static string OutputNameFor(string filename, BuildOptions options)
    {
        var extension = options.ImageFormatKind == BuildImageFormatKind.Jpeg ? ".jpg" : ".png";
        return Path.ChangeExtension(filename, extension);
    }

    /// <summary>Writes out one page into the temporary directory.</summary>
    /// <returns>true when it was copied without re-encoding.</returns>
    static bool RenderPage(
        string directoryPath,
        string tempDirPath,
        string filename,
        string outputName,
        BuildOptions options,
        Func<string, bool>? canUseVerbatim)
    {
        var outputPath = Path.Combine(tempDirPath, outputName);
        var sourcePath = Path.Combine(directoryPath, filename);

        if (options.CanCopyWithoutReEncoding && (canUseVerbatim?.Invoke(sourcePath) ?? true))
        {
            // Original size, no corner dots and JPEG output, so the bytes can be stored verbatim.
            // The EXIF orientation tag is left untouched as well, so viewers that honor EXIF will
            // display the page in the correct orientation.
            File.Copy(sourcePath, outputPath, overwrite: true);
            return true;
        }

        // When re-encoding, the EXIF orientation is baked into the pixels here.
        // No orientation tag is written to the output, so the page is never rotated twice.
        var fitWithin = options.SizeKind == BuildSizeKind.Specified ? options.TargetSize : (ImageSize?)null;
        using var bitmap = PageImagePipeline.DecodeOriented(sourcePath, fitWithin);

        if (options.DrawsCornerDots) PageImagePipeline.DrawCornerDots(bitmap);

        PageImagePipeline.Encode(
            bitmap,
            outputPath,
            options.ImageFormatKind == BuildImageFormatKind.Jpeg ? OutputImageFormat.Jpeg : OutputImageFormat.Png,
            options.JpegQuality);
        return false;
    }

    static void TryDeleteDirectory(string path)
    {
        // Cleanup of the temporary directory. Failure does not affect the result of the main work.
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
