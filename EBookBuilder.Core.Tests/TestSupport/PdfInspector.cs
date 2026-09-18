using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>One object read back out of a PDF.</summary>
/// <param name="Id">The object number.</param>
/// <param name="Dictionary">The object's text, up to the start of its stream.</param>
/// <param name="Payload">The stream payload, or null when the object has no stream.</param>
public sealed record PdfObject(int Id, string Dictionary, byte[]? Payload);

/// <summary>
/// Reads a written PDF back so tests can assert on it.
/// </summary>
/// <remarks>
/// <para>
/// The bytes are kept as Latin-1 text, where one byte is always one character, so the offsets into
/// the text are the offsets into the file. That makes it possible to follow the cross-reference
/// table back into the raw bytes.
/// </para>
/// <para>
/// A stream payload is cut out using the <c>/Length</c> the writer declared rather than by
/// searching for <c>endstream</c>, because a JPEG may contain that byte sequence by chance. A
/// wrong <c>/Length</c> therefore shows up as a failed comparison instead of being papered over.
/// </para>
/// </remarks>
public sealed class PdfInspector
{
    static readonly Regex ObjectHeader = new(@"(?m)^(\d+) 0 obj\n", RegexOptions.Compiled);
    static readonly Regex LengthEntry = new(@"/Length (\d+)", RegexOptions.Compiled);

    readonly byte[] m_Bytes;

    PdfInspector(byte[] bytes, string text, IReadOnlyList<PdfObject> objects)
    {
        m_Bytes = bytes;
        Text = text;
        Objects = objects;
    }

    /// <summary>The whole file as Latin-1 text.</summary>
    public string Text { get; }

    /// <summary>Every object in the file, in the order it appears.</summary>
    public IReadOnlyList<PdfObject> Objects { get; }

    /// <summary>The objects holding an image.</summary>
    public IReadOnlyList<PdfObject> Images =>
        Objects.Where(o => o.Dictionary.Contains("/Subtype /Image", StringComparison.Ordinal)).ToArray();

    /// <summary>The page dictionaries.</summary>
    public IReadOnlyList<string> Pages =>
        Objects.Where(o => o.Dictionary.Contains("/Type /Page ", StringComparison.Ordinal))
               .Select(o => o.Dictionary).ToArray();

    /// <summary>The content streams, which hold the page placements.</summary>
    /// <remarks>An image and a content stream are the only objects carrying a payload.</remarks>
    public IReadOnlyList<string> ContentStreams =>
        Objects.Where(o => o.Payload is not null
                        && !o.Dictionary.Contains("/Subtype /Image", StringComparison.Ordinal))
               .Select(o => Encoding.Latin1.GetString(o.Payload!)).ToArray();

    /// <summary>Reads a PDF from a file.</summary>
    public static PdfInspector Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Reads a PDF from bytes.</summary>
    public static PdfInspector Parse(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var objects = new List<PdfObject>();

        foreach (Match header in ObjectHeader.Matches(text))
        {
            var id = int.Parse(header.Groups[1].Value, CultureInfo.InvariantCulture);
            var bodyStart = header.Index + header.Length;

            var streamIndex = text.IndexOf("stream\n", bodyStart, StringComparison.Ordinal);
            var firstEndObject = text.IndexOf("endobj", bodyStart, StringComparison.Ordinal);
            if (streamIndex < 0 || firstEndObject < streamIndex)
            {
                // No stream: the dictionary runs to endobj.
                objects.Add(new PdfObject(id, text[bodyStart..firstEndObject], null));
                continue;
            }

            var dictionary = text[bodyStart..streamIndex];
            var length = int.Parse(
                LengthEntry.Match(dictionary).Groups[1].Value, CultureInfo.InvariantCulture);

            var payloadStart = streamIndex + "stream\n".Length;
            var payload = bytes[payloadStart..(payloadStart + length)];
            objects.Add(new PdfObject(id, dictionary, payload));
        }

        return new PdfInspector(bytes, text, objects);
    }

    /// <summary>The offset the <c>startxref</c> entry points at.</summary>
    public long StartXrefOffset
    {
        get
        {
            var match = Regex.Match(Text, @"startxref\n(\d+)\n");
            Assert.True(match.Success, "The file has no startxref entry.");
            return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The cross-reference entries after the free one, as byte offsets in object order.
    /// </summary>
    /// <remarks>
    /// The entries are read as fixed 20 byte records rather than by pattern, because that is how a
    /// reader finds them. A single extra byte in one entry shifts every entry after it, which a
    /// pattern match would happily follow along with.
    /// </remarks>
    public IReadOnlyList<long> XrefOffsets
    {
        get
        {
            var match = Regex.Match(Text, @"xref\n(\d+) (\d+)\n");
            Assert.True(match.Success, "The file has no cross-reference table.");

            var start = match.Index + match.Length;
            var count = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            var offsets = new List<long>();
            for (var i = 0; i < count; ++i)
            {
                var entry = Text.Substring(start + i * 20, 20);

                if (i == 0)
                {
                    Assert.Equal("0000000000 65535 f\r\n", entry);
                    continue;
                }

                Assert.EndsWith(" 00000 n\r\n", entry, StringComparison.Ordinal);
                offsets.Add(long.Parse(entry[..10], CultureInfo.InvariantCulture));
            }

            return offsets;
        }
    }

    /// <summary>The bytes at an offset, as Latin-1 text, stopping at the end of the file.</summary>
    public string TextAt(long offset, int length) =>
        Encoding.Latin1.GetString(m_Bytes, (int)offset, Math.Min(length, m_Bytes.Length - (int)offset));
}
