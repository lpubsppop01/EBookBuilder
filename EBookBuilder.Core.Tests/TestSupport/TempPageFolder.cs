using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>
/// A page folder for tests. Deleted automatically on dispose.
/// </summary>
public sealed class TempPageFolder : IDisposable
{
    bool m_Disposed;

    /// <summary>Full path of the folder.</summary>
    public string Path { get; }

    TempPageFolder(string path) => Path = path;

    /// <summary>Creates an empty folder.</summary>
    public static TempPageFolder CreateEmpty()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ebookbuilder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempPageFolder(path);
    }

    /// <summary>Creates a folder holding dummy JPEGs with the given filenames, used in place of serial numbers.</summary>
    public static TempPageFolder CreateWithPages(params string[] filenames)
    {
        var folder = CreateEmpty();
        foreach (var filename in filenames)
        {
            TestImages.WriteTwoToneJpeg(folder.FilePath(filename), 80, 120);
        }
        return folder;
    }

    /// <summary>Creates <paramref name="count"/> pages with serial filenames.</summary>
    /// <param name="makeDistinct">
    /// When true, the pattern is varied per page. Reordering tests need to follow
    /// which page moved where by its content.
    /// </param>
    /// <remarks>The digit count is determined by the page count (10 pages or more use <c>00.jpg</c>, 100 or more use <c>000.jpg</c>).</remarks>
    public static TempPageFolder CreateWithSerialPages(int count, int width = 80, int height = 120, bool makeDistinct = false)
    {
        var folder = CreateEmpty();
        for (var i = 0; i < count; ++i)
        {
            var splitRatio = makeDistinct ? (i + 1.0) / (count + 1) : 0.5;
            TestImages.WriteTwoToneJpeg(
                folder.FilePath(PageNaming.FormatFilename(i, count)), width, height, splitRatio: splitRatio);
        }
        return folder;
    }

    /// <summary>Returns the content hashes of the pages in their current order.</summary>
    /// <remarks>Used to follow which content moved to which position before and after reordering.</remarks>
    public IReadOnlyList<string> ContentHashes() =>
        EnumerateFilenames().Select(name => TestImages.ContentHash(FilePath(name))).ToArray();

    /// <summary>Returns the pixel hashes of the pages in their current order.</summary>
    /// <remarks>
    /// Rewriting EXIF tags changes the file bytes (tags are added and lengths change).
    /// When you want to check whether image quality is preserved, use this decoded result instead.
    /// </remarks>
    public IReadOnlyList<string> PixelHashes() =>
        EnumerateFilenames().Select(name => TestImages.PixelHash(FilePath(name))).ToArray();

    /// <summary>Returns the full path of a file in the folder.</summary>
    public string FilePath(string filename) => System.IO.Path.Combine(Path, filename);

    /// <summary>The filenames in page order.</summary>
    public IReadOnlyList<string> EnumerateFilenames() => PageFolder.EnumeratePageFilenames(Path);

    /// <summary>Returns all filenames directly under the folder, in any order.</summary>
    public IReadOnlyList<string> AllFilenames() =>
        new DirectoryInfo(Path).GetFiles().Select(f => f.Name).ToArray();

    public void Dispose()
    {
        if (m_Disposed) return;
        m_Disposed = true;
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // This is test cleanup, so swallow failures
        }
    }
}
