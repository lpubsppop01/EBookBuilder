using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>A page folder for tests. Deleted automatically on dispose.</summary>
public sealed class TestPages : IDisposable
{
    bool m_Disposed;

    /// <summary>Full path of the folder.</summary>
    public string Path { get; }

    TestPages(string path) => Path = path;

    /// <summary>Creates <paramref name="count"/> pages with serial names starting from <c>0.jpg</c>.</summary>
    public static TestPages Create(int count, int width = 60, int height = 90)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ebookbuilder-app-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        for (var i = 0; i < count; ++i)
        {
            WriteJpeg(System.IO.Path.Combine(path, $"{i}.jpg"), width, height);
        }

        return new TestPages(path);
    }

    static void WriteJpeg(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

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
