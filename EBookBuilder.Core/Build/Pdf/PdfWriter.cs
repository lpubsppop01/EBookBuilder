using System.Globalization;
using System.Text;

namespace Lpubsppop01.EBookBuilder.Core.Build.Pdf;

/// <summary>Writes a PDF as a forward-only byte stream.</summary>
/// <remarks>
/// <para>
/// Everything goes out as bytes, never through a text writer: an encoding or a newline translation
/// would change the byte count and every <c>/Length</c> in the file would then be wrong.
/// </para>
/// <para>
/// The layout is deliberately the most conservative corner of the format — a PDF 1.4 header, a
/// classic cross-reference table, no object streams and no compression of the content streams.
/// That is the subset every reader and every repair heuristic handles.
/// </para>
/// </remarks>
public sealed class PdfWriter
{
    readonly Stream m_Stream;
    readonly Dictionary<int, long> m_Offsets = [];
    readonly HashSet<int> m_Written = [];
    int m_NextObjectId = 1;

    /// <summary>Writes to a stream.</summary>
    public PdfWriter(Stream stream) => m_Stream = stream;

    /// <summary>The number of objects allocated so far.</summary>
    public int ObjectCount => m_NextObjectId - 1;

    /// <summary>
    /// Formats a number for the PDF syntax.
    /// </summary>
    /// <remarks>
    /// The invariant culture is required, not cosmetic: this machine's culture decides the decimal
    /// separator, and a locale that writes <c>0,24</c> would put a comma into the content stream
    /// and break the file.
    /// </remarks>
    public static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Writes the file header.</summary>
    public void WriteHeader()
    {
        WriteAscii("%PDF-1.4\n");

        // A comment whose bytes are above 127, so tools that sniff the content treat the file as binary.
        WriteBytes([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);
    }

    /// <summary>Reserves an object number. Numbers are handed out in order, starting at 1.</summary>
    public int AllocateObjectId() => m_NextObjectId++;

    /// <summary>Writes an object whose body is written out as it is.</summary>
    public void WriteObject(int id, string body)
    {
        BeginObject(id);
        WriteAscii(body);
        WriteAscii("\nendobj\n");
    }

    /// <summary>
    /// Writes an object holding a stream.
    /// </summary>
    /// <param name="id">The object number, as returned by <see cref="AllocateObjectId"/>.</param>
    /// <param name="dictionaryEntries">
    /// The dictionary entries, without the enclosing brackets and without <c>/Length</c>. It is
    /// appended here from the real payload size, so a caller cannot declare the wrong length.
    /// </param>
    /// <param name="data">The stream payload.</param>
    public void WriteStreamObject(int id, string dictionaryEntries, ReadOnlySpan<byte> data)
    {
        BeginObject(id);
        WriteAscii($"<< {dictionaryEntries} /Length {data.Length} >>\nstream\n");
        WriteBytes(data);
        WriteAscii("\nendstream\nendobj\n");
    }

    /// <summary>Writes the cross-reference table and the trailer, finishing the file.</summary>
    /// <exception cref="InvalidOperationException">When an allocated object was never written.</exception>
    public void WriteXrefAndTrailer(int rootObjectId)
    {
        // A missing object makes the cross-reference table point at nothing, which is the classic
        // way to produce a file that will not open, so it is turned into a loud failure here.
        if (m_Written.Count != ObjectCount)
            throw new InvalidOperationException(
                $"{ObjectCount} objects were allocated but {m_Written.Count} were written.");

        var xrefOffset = m_Stream.Position;

        WriteAscii("xref\n");
        WriteAscii(string.Create(CultureInfo.InvariantCulture, $"0 {m_NextObjectId}\n"));

        // Every entry is exactly 20 bytes, terminator included: 10 digits, a space, 5 digits,
        // a space, the type and a two character end of line. A reader finds the entries by
        // multiplying, so a single extra byte shifts every entry after it.
        WriteAscii("0000000000 65535 f\r\n");
        for (var id = 1; id < m_NextObjectId; ++id)
        {
            WriteAscii(m_Offsets[id].ToString("D10", CultureInfo.InvariantCulture));
            WriteAscii(" 00000 n\r\n");
        }

        WriteAscii("trailer\n");
        WriteAscii(string.Create(CultureInfo.InvariantCulture, $"<< /Size {m_NextObjectId} /Root {rootObjectId} 0 R >>\n"));
        WriteAscii("startxref\n");
        WriteAscii(string.Create(CultureInfo.InvariantCulture, $"{xrefOffset}\n"));
        WriteAscii("%%EOF\n");
    }

    void BeginObject(int id)
    {
        if (id < 1 || id >= m_NextObjectId)
            throw new ArgumentOutOfRangeException(nameof(id), id, "The object number was not allocated.");

        if (!m_Written.Add(id))
            throw new InvalidOperationException($"Object {id} was already written.");

        m_Offsets[id] = m_Stream.Position;
        WriteAscii(string.Create(CultureInfo.InvariantCulture, $"{id} 0 obj\n"));
    }

    void WriteAscii(string text) => m_Stream.Write(Encoding.ASCII.GetBytes(text));

    void WriteBytes(ReadOnlySpan<byte> bytes) => m_Stream.Write(bytes);
}
