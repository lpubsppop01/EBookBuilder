using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>
/// A temporary JPEG file for tests. Deleted automatically on dispose.
/// </summary>
/// <remarks>Creates a simple image that is red on the left half and blue on the right half.</remarks>
public sealed class TempJpeg : IDisposable
{
    readonly string m_DirectoryPath;
    bool m_Disposed;

    /// <summary>Full path of the created JPEG.</summary>
    public string Path { get; }

    /// <summary>The original size at creation time.</summary>
    public (int Width, int Height) Size { get; }

    TempJpeg(string directoryPath, string path, (int, int) size)
    {
        m_DirectoryPath = directoryPath;
        Path = path;
        Size = size;
    }

    /// <summary>Creates a new temporary JPEG of the specified size.</summary>
    public static TempJpeg Create(int width = 80, int height = 120, int quality = 100)
        => CreateCore(width, height, quality, exifBigEndian: null);

    /// <summary>
    /// Creates a new temporary JPEG that already carries an EXIF block, the way a page straight
    /// from a camera or a scanner does.
    /// </summary>
    /// <param name="bigEndian">Byte order of the EXIF block that is already in the file.</param>
    public static TempJpeg CreateWithExif(bool bigEndian = true, int width = 80, int height = 120, int quality = 100)
        => CreateCore(width, height, quality, exifBigEndian: bigEndian);

    static TempJpeg CreateCore(int width, int height, int quality, bool? exifBigEndian)
    {
        var directoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ebookbuilder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        var path = System.IO.Path.Combine(directoryPath, "page.jpg");
        if (exifBigEndian is { } bigEndian)
            TestImages.WriteTwoToneJpegWithExif(path, width, height, bigEndian, quality);
        else
            TestImages.WriteTwoToneJpeg(path, width, height, quality);

        return new TempJpeg(directoryPath, path, (width, height));
    }

    /// <summary>Returns the decoded pixels as a BGRA byte array (used to verify losslessness).</summary>
    public byte[] ReadPixelBytes()
    {
        using var bitmap = SKBitmap.Decode(Path);
        return bitmap.GetPixelSpan().ToArray();
    }

    /// <summary>Returns the size of the decoded image.</summary>
    public (int Width, int Height) ReadSize()
    {
        using var bitmap = SKBitmap.Decode(Path);
        return (bitmap.Width, bitmap.Height);
    }

    public void Dispose()
    {
        if (m_Disposed) return;
        m_Disposed = true;
        try
        {
            if (Directory.Exists(m_DirectoryPath)) Directory.Delete(m_DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
            // This is test cleanup, so swallow failures
        }
    }
}
