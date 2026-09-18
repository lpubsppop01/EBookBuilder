using System.IO.Compression;
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
    /// <summary>
    /// Writes out the pages and creates a CBZ. An existing output file is overwritten.
    /// </summary>
    /// <exception cref="ArgumentException">When the settings cannot be built.</exception>
    /// <exception cref="InvalidOperationException">When the filenames are not serial numbers.</exception>
    public static async Task<BuildResult> BuildAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        BuildOptions options,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        using var staged = await PageStager.CreateAsync(
            directoryPath, filenames, options, progress: progress, cancellationToken: cancellationToken);

        WriteZip(staged.DirectoryPath, options.OutputFilePath, staged.Pages.Select(page => page.Name));

        return new BuildResult(options.OutputFilePath, filenames.Count, staged.CopiedCount);
    }

    static void WriteZip(string sourceDirectoryPath, string outputFilePath, IEnumerable<string> entryNames)
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
}
