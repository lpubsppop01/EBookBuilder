using System.IO.Compression;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>
/// Packages page images together and writes out a CBZ (Comic Book ZIP).
/// </summary>
/// <remarks>
/// <para>
/// Images are placed directly under the ZIP root (<c>includeBaseDirectory: false</c>).
/// This is the layout CBZ readers expect.
/// </para>
/// <para>
/// The compression level is no compression. The contents are JPEG/PNG and thus already
/// compressed, so applying deflate would not shrink them and would only cost time.
/// </para>
/// </remarks>
public static class CbzBuilder
{
    /// <summary>Parent of the temporary working directories.</summary>
    static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "lpubsppop01.EBookBuilder");

    /// <summary>
    /// Writes out the pages and creates a CBZ. An existing output file is overwritten.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the filenames are not serial numbers.</exception>
    public static async Task<BuildResult> BuildAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        BuildOptions options,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
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

            await Parallel.ForEachAsync(
                Enumerable.Range(0, filenames.Count),
                cancellationToken,
                (index, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var copied = RenderPage(directoryPath, tempDirPath, filenames[index], outputNames[index], options);

                    lock (countLock)
                    {
                        if (copied) ++copiedCount;
                        progress?.Report(new PageProgress(++done, filenames.Count));
                    }

                    return ValueTask.CompletedTask;
                }).ConfigureAwait(false);

            WriteZip(tempDirPath, options.OutputFilePath, outputNames);

            return new BuildResult(options.OutputFilePath, filenames.Count, copiedCount);
        }
        finally
        {
            TryDeleteDirectory(tempDirPath);
        }
    }

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
        BuildOptions options)
    {
        var outputPath = Path.Combine(tempDirPath, outputName);

        if (options.CanCopyWithoutReEncoding)
        {
            // Original size, no corner dots and JPEG output, so the bytes can be stored verbatim.
            // The EXIF orientation tag is left untouched as well, so viewers that honor EXIF will
            // display the page in the correct orientation.
            File.Copy(Path.Combine(directoryPath, filename), outputPath, overwrite: true);
            return true;
        }

        // When re-encoding, the EXIF orientation is baked into the pixels here.
        // No orientation tag is written to the output, so the page is never rotated twice.
        var fitWithin = options.SizeKind == BuildSizeKind.Specified ? options.TargetSize : (ImageSize?)null;
        using var bitmap = PageImagePipeline.DecodeOriented(Path.Combine(directoryPath, filename), fitWithin);

        if (options.DrawsCornerDots) PageImagePipeline.DrawCornerDots(bitmap);

        PageImagePipeline.Encode(
            bitmap,
            outputPath,
            options.ImageFormatKind == BuildImageFormatKind.Jpeg ? OutputImageFormat.Jpeg : OutputImageFormat.Png,
            options.JpegQuality);
        return false;
    }

    static void WriteZip(string sourceDirectoryPath, string outputFilePath, IReadOnlyList<string> entryNames)
    {
        var outputDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(outputFilePath));
        if (!string.IsNullOrEmpty(outputDirectoryPath)) Directory.CreateDirectory(outputDirectoryPath);

        if (File.Exists(outputFilePath)) File.Delete(outputFilePath);

        // Entries are added in page order.
        // ZipFile.CreateFromDirectory builds the archive by enumerating the directory, so the
        // order inside the archive would depend on the order the file system returns and become
        // nondeterministic. For CBZ readers that read pages in archive order, that means pages
        // end up in the wrong order.
        using var archive = ZipFile.Open(outputFilePath, ZipArchiveMode.Create);
        foreach (var entryName in entryNames)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            using var sourceStream = File.OpenRead(Path.Combine(sourceDirectoryPath, entryName));
            sourceStream.CopyTo(entryStream);
        }
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
