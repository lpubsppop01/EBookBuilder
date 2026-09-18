namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>
/// A temporary file with exactly the bytes given. Deleted automatically on dispose.
/// </summary>
/// <remarks>Used where the file has to be malformed on purpose, which the image helpers cannot produce.</remarks>
public sealed class TempFile : IDisposable
{
    readonly string m_DirectoryPath;
    bool m_Disposed;

    /// <summary>Full path of the created file.</summary>
    public string Path { get; }

    TempFile(string directoryPath, string path)
    {
        m_DirectoryPath = directoryPath;
        Path = path;
    }

    /// <summary>Creates a new temporary file holding the given bytes.</summary>
    public static TempFile WithBytes(byte[] bytes, string filename = "data.bin")
    {
        var directoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ebookbuilder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        var path = System.IO.Path.Combine(directoryPath, filename);
        File.WriteAllBytes(path, bytes);

        return new TempFile(directoryPath, path);
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
