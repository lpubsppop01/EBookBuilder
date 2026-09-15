using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Pages;

/// <summary>Deleting, duplicating, reordering and rotating pages.</summary>
/// <remarks>
/// <para>
/// Every operation takes a "list of filenames in page order" and returns the list after the
/// operation. It is decoupled from the UI, so the same processing can be called the same way
/// from both the GUI and the CLI.
/// </para>
/// <para>
/// Operations that change filenames finally align them to serial numbers
/// (the rules of <see cref="PageNaming"/>). Same behavior as the original.
/// </para>
/// </remarks>
public static class PageOperations
{
    /// <summary>
    /// Rotates the specified page one step clockwise. In practice the pixels are not rotated;
    /// the EXIF orientation tag is rewritten.
    /// </summary>
    /// <remarks>
    /// No re-encoding occurs, so the image quality does not degrade.
    /// The same holds no matter how many times this is repeated.
    /// </remarks>
    public static void Rotate(
        string directoryPath,
        string filename,
        RotationAmount amount)
    {
        if (amount == RotationAmount.None) return;

        var path = Path.Combine(directoryPath, filename);
        var orientation = ExifOrientationStore.Read(path);
        ExifOrientationStore.Write(path, orientation.Rotated(amount));
    }

    /// <summary>Rotates multiple pages at once. The processing is done in parallel.</summary>
    public static async Task RotateAllAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        RotationAmount amount,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (amount == RotationAmount.None) return;

        var done = 0;
        var countLock = new object();

        await Parallel.ForEachAsync(filenames, cancellationToken, (filename, token) =>
        {
            token.ThrowIfCancellationRequested();
            Rotate(directoryPath, filename, amount);

            // The report is made after completion, so 100% is reached on the last item.
            int current;
            lock (countLock) current = ++done;
            progress?.Report(new PageProgress(current, filenames.Count));

            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Crops the page by specifying margins. The area left after removing the specified margins
    /// remains.
    /// </summary>
    /// <param name="directoryPath">Folder containing the pages.</param>
    /// <param name="filename">Filename of the target.</param>
    /// <param name="left">Width to remove from the left.</param>
    /// <param name="top">Height to remove from the top.</param>
    /// <param name="right">Width to remove from the right.</param>
    /// <param name="bottom">Height to remove from the bottom.</param>
    /// <param name="jpegQuality">Quality used when writing out JPEG.</param>
    /// <remarks>
    /// <para>
    /// Cropping works on pixels and therefore involves re-encoding. The EXIF orientation is
    /// baked into the pixels at that point, and no orientation tag is written to the output,
    /// so there is no double rotation.
    /// </para>
    /// <para>
    /// Cropping in the original had a bug and in practice did nothing. The temporary file was
    /// created with <c>Path.GetTempFileName()</c>, which gave it a <c>.tmp</c> extension, so
    /// saving raised an exception about an unsupported format (and on top of that, the progress
    /// dialog swallowed the exception). Here a temporary file that keeps the original extension
    /// is used.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">When the margins are too large and no crop area remains.</exception>
    public static void Crop(
        string directoryPath,
        string filename,
        int left,
        int top,
        int right,
        int bottom,
        int jpegQuality = PageImagePipeline.DefaultJpegQuality)
    {
        if (left < 0 || top < 0 || right < 0 || bottom < 0)
            throw new ArgumentOutOfRangeException(nameof(left), "Negative margins cannot be specified.");

        var sourcePath = Path.Combine(directoryPath, filename);
        var outputFormat = PageImagePipeline.FormatFromExtension(sourcePath);

        // The temporary file uses the same folder and the same extension as the source.
        // The save format is determined by the extension, so a different extension could not be
        // written out.
        var tempPath = Path.Combine(
            directoryPath,
            Path.GetFileNameWithoutExtension(filename) + ".cropping" + Path.GetExtension(filename));

        try
        {
            using (var source = PageImagePipeline.DecodeOriented(sourcePath))
            {
                var width = source.Width - (left + right);
                var height = source.Height - (top + bottom);
                if (width <= 0 || height <= 0)
                    throw new InvalidOperationException("The margins are too large. No area remains after cropping.");

                using var cropped = PageImagePipeline.Crop(source, left, top, width, height);
                PageImagePipeline.Encode(cropped, tempPath, outputFormat, jpegQuality);
            }

            File.Move(tempPath, sourcePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Renumbers to serial numbers in the given order.
    /// </summary>
    /// <returns>The filename list after renumbering.</returns>
    /// <remarks>
    /// <para>
    /// It is done in two passes because names can collide. First the names are changed to the
    /// target names from back to front, and any name that is already taken is evacuated with a
    /// <c>temp_</c> prefix added; the second pass removes the <c>temp_</c>. This is the same
    /// procedure as in the original.
    /// </para>
    /// <para>
    /// The original removed the prefix with <c>Replace("temp_", "")</c>, which breaks when
    /// <c>temp_</c> appears in the middle of a filename, so here only the leading prefix is
    /// removed.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> RenameWithSerialNumbers(
        string directoryPath,
        IReadOnlyList<string> filenames,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = filenames.ToArray();
        var count = result.Length;
        var done = 0;

        // First pass: move to the target names. If a name is taken, prefix it with temp_ to
        // evacuate it.
        for (var i = count - 1; i >= 0; --i)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputFilename = PageNaming.FormatFilename(i, count);
            if (outputFilename != result[i])
            {
                var outputPath = Path.Combine(directoryPath, outputFilename);
                if (File.Exists(outputPath))
                {
                    outputFilename = PageNaming.TempPrefix + outputFilename;
                    outputPath = Path.Combine(directoryPath, outputFilename);
                }
                File.Move(Path.Combine(directoryPath, result[i]), outputPath);
                result[i] = outputFilename;
            }

            progress?.Report(new PageProgress(++done, count));
        }

        // Second pass: remove the temp_ prefix.
        for (var i = count - 1; i >= 0; --i)
        {
            var name = result[i];
            if (!name.StartsWith(PageNaming.TempPrefix, StringComparison.Ordinal)) continue;

            var outputFilename = name[PageNaming.TempPrefix.Length..];
            File.Move(Path.Combine(directoryPath, name), Path.Combine(directoryPath, outputFilename));
            result[i] = outputFilename;
        }

        return result;
    }

    /// <summary>
    /// Duplicates the specified page and renumbers the serial numbers.
    /// </summary>
    /// <param name="directoryPath">Folder containing the pages.</param>
    /// <param name="filenames">Filenames in the current page order.</param>
    /// <param name="index">Position of the page to duplicate.</param>
    /// <param name="toLast">If true, insert at the end; if false, insert right after the source page.</param>
    /// <param name="progress">Destination for progress notifications.</param>
    /// <param name="cancellationToken">For cancellation.</param>
    /// <returns>The filename list after the operation.</returns>
    /// <exception cref="InvalidOperationException">When the temporary file for duplication already exists.</exception>
    public static IReadOnlyList<string> Duplicate(
        string directoryPath,
        IReadOnlyList<string> filenames,
        int index,
        bool toLast,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var copyFilename = DuplicateSourceFilename;
        var copyPath = Path.Combine(directoryPath, copyFilename);
        if (File.Exists(copyPath))
            throw new InvalidOperationException($"The temporary file for duplication already exists: {copyPath}");

        File.Copy(Path.Combine(directoryPath, filenames[index]), copyPath);

        var list = filenames.ToList();
        if (toLast) list.Add(copyFilename);
        else list.Insert(index + 1, copyFilename);

        return RenameWithSerialNumbers(directoryPath, list, progress, cancellationToken);
    }

    /// <summary>Moves the specified page to the end and renumbers the serial numbers.</summary>
    /// <returns>The filename list after the operation.</returns>
    public static IReadOnlyList<string> MoveToLast(
        string directoryPath,
        IReadOnlyList<string> filenames,
        int index,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var list = filenames.ToList();
        var moved = list[index];
        list.RemoveAt(index);
        list.Add(moved);

        return RenameWithSerialNumbers(directoryPath, list, progress, cancellationToken);
    }

    /// <summary>
    /// Deletes the page file. The serial numbers are not closed up.
    /// </summary>
    /// <remarks>
    /// In the original, deleting leaves gaps in the serial numbers. To keep editing, the user
    /// explicitly runs the serial number rename. This behavior is preserved.
    /// </remarks>
    public static void Delete(string directoryPath, string filename) =>
        File.Delete(Path.Combine(directoryPath, filename));

    /// <summary>Temporary filename used when duplicating.</summary>
    internal const string DuplicateSourceFilename = "copy.jpg";
}
