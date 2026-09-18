namespace Lpubsppop01.EBookBuilder.Core.Build.Pdf;

/// <summary>Reads the frame header of a JPEG file.</summary>
/// <remarks>
/// An image XObject stored with <c>/DCTDecode</c> has to declare a colour space that matches the
/// JPEG's own component count. SkiaSharp only reports the format a file decodes to, not how many
/// components it stores, so the markers have to be walked directly. The walk seeks over each
/// segment by its declared length rather than buffering, because a scanned page carries an EXIF
/// thumbnail in its APP1 segment and the frame header can be tens of kilobytes in.
/// </remarks>
public static class JpegHeader
{
    const byte StartOfImage = 0xD8;
    const byte EndOfImage = 0xD9;
    const byte StartOfScan = 0xDA;
    const byte MarkerPrefix = 0xFF;

    /// <summary>
    /// Number of colour components: 1 for grayscale, 3 for YCbCr/RGB.
    /// </summary>
    /// <returns>0 when the file is not a JPEG or its frame header cannot be read.</returns>
    public static int ReadComponentCount(string path)
    {
        using var stream = File.OpenRead(path);
        return ReadComponentCount(stream);
    }

    /// <summary>Whether the file can be stored in a PDF as it is.</summary>
    /// <remarks>
    /// One and three component JPEGs map onto <c>/DeviceGray</c> and <c>/DeviceRGB</c> directly.
    /// Adobe CMYK (four components) does not: it also needs an inverted <c>/Decode</c> array, so
    /// such a page is re-encoded rather than embedded.
    /// </remarks>
    public static bool IsEmbeddable(string path) => ReadComponentCount(path) is 1 or 3;

    static int ReadComponentCount(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];

        if (!TryReadExactly(stream, buffer[..2])) return 0;
        if (buffer[0] != MarkerPrefix || buffer[1] != StartOfImage) return 0;

        while (true)
        {
            if (!TryReadExactly(stream, buffer[..2])) return 0;
            if (buffer[0] != MarkerPrefix) return 0;

            var marker = buffer[1];
            if (marker == MarkerPrefix)
            {
                // A fill byte was inserted before the marker; step back and read it again.
                stream.Seek(-1, SeekOrigin.Current);
                continue;
            }

            if (IsStandalone(marker)) continue;

            if (!TryReadExactly(stream, buffer[..2])) return 0;
            var length = (buffer[0] << 8) | buffer[1];
            if (length < 2) return 0;

            if (IsStartOfFrame(marker))
            {
                // precision(1) height(2) width(2) components(1)
                if (!TryReadExactly(stream, buffer[..6])) return 0;
                return buffer[5];
            }

            // The scan follows the frame header, so one must have been seen by now.
            if (marker == StartOfScan) return 0;

            if (stream.Seek(length - 2, SeekOrigin.Current) < 0) return 0;
        }
    }

    /// <summary>Whether the marker carries no length field.</summary>
    static bool IsStandalone(byte marker) =>
        marker is StartOfImage or EndOfImage or 0x01 || marker is >= 0xD0 and <= 0xD7;

    /// <summary>
    /// Whether the marker begins a frame. The reserved entries inside the range are not frames:
    /// <c>0xC4</c> is a Huffman table, <c>0xC8</c> is reserved and <c>0xCC</c> is arithmetic coding.
    /// </summary>
    static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);

    static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        try
        {
            stream.ReadExactly(buffer);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }
}
