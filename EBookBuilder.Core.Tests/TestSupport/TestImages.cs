using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

/// <summary>Image generation and checks for tests.</summary>
internal static class TestImages
{
    /// <summary>
    /// Writes a JPEG that is red on the left and blue on the right.
    /// </summary>
    /// <param name="splitRatio">
    /// Position of the boundary between red and blue (0 to 1). Vary it per page when pages need to be told apart.
    /// </param>
    /// <remarks>
    /// Because the colors are split left and right, rotation (which edge moves where) and
    /// cropping (which color comes out) can be followed by eye.
    /// </remarks>
    public static void WriteTwoToneJpeg(string path, int width, int height, int quality = 100, double splitRatio = 0.5)
    {
        var splitX = (float)(width * splitRatio);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Red);
            canvas.DrawRect(new SKRect(splitX, 0, width, height), new SKPaint { Color = SKColors.Blue });
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Writes a two-tone JPEG that already carries an EXIF block, the way a file straight from a
    /// camera or a scanner does.
    /// </summary>
    /// <param name="bigEndian">
    /// Byte order of the EXIF block. Camera and scanner output is often big-endian
    /// (<c>MM</c>), while the block this app writes for a file that has none is little-endian
    /// (<c>II</c>).
    /// </param>
    /// <remarks>
    /// Rewriting the orientation keeps the byte order of the block that is already in the file,
    /// so both orders have to work. The tag starts out as Orientation 1 (horizontal normal),
    /// which is what a freshly scanned page has.
    /// </remarks>
    public static void WriteTwoToneJpegWithExif(
        string path,
        int width,
        int height,
        bool bigEndian = true,
        int quality = 100)
    {
        WriteTwoToneJpeg(path, width, height, quality);

        var jpeg = File.ReadAllBytes(path);
        var exif = BuildExifSegmentWithOrientation(bigEndian);

        // APP1 goes after SOI, and after APP0 when there is one, which is the order camera
        // files use.
        var insertAt = jpeg.Length >= 6 && jpeg[2] == 0xFF && jpeg[3] == 0xE0
            ? 4 + ((jpeg[4] << 8) | jpeg[5])
            : 2;

        using var output = File.Create(path);
        output.Write(jpeg, 0, insertAt);
        output.Write(exif);
        output.Write(jpeg, insertAt, jpeg.Length - insertAt);
    }

    /// <summary>Builds an APP1 (EXIF) segment holding only an Orientation tag with the value 1.</summary>
    static byte[] BuildExifSegmentWithOrientation(bool bigEndian)
    {
        var body = new List<byte>();
        body.AddRange("Exif"u8.ToArray());
        body.AddRange([0x00, 0x00]);

        // TIFF header: byte order, magic number 42, and the offset of IFD0.
        body.AddRange(bigEndian ? "MM"u8.ToArray() : "II"u8.ToArray());
        AddUInt16(body, 42, bigEndian);
        AddUInt32(body, 8, bigEndian);

        // IFD0 with a single entry: Orientation, type SHORT, count 1, value 1.
        AddUInt16(body, 1, bigEndian);                 // number of entries
        AddUInt16(body, 0x0112, bigEndian);            // tag: Orientation
        AddUInt16(body, 3, bigEndian);                 // type: SHORT
        AddUInt32(body, 1, bigEndian);                 // count
        AddUInt16(body, 1, bigEndian);                 // value 1, in the first two bytes
        AddUInt16(body, 0, bigEndian);                 // of the four byte value field
        AddUInt32(body, 0, bigEndian);                 // no next IFD

        var segment = new List<byte> { 0xFF, 0xE1 };
        // The length of a JPEG segment is always big-endian, whatever the EXIF block uses.
        AddUInt16(segment, body.Count + 2, bigEndian: true);
        segment.AddRange(body);
        return segment.ToArray();
    }

    static void AddUInt16(List<byte> bytes, int value, bool bigEndian)
    {
        var b = new[] { (byte)(value >> 8), (byte)value };
        bytes.AddRange(bigEndian ? b : b.Reverse());
    }

    static void AddUInt32(List<byte> bytes, int value, bool bigEndian)
    {
        var b = new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        bytes.AddRange(bigEndian ? b : b.Reverse());
    }

    /// <summary>
    /// Writes a grayscale JPEG that is black on the left and white on the right.
    /// </summary>
    /// <remarks>
    /// A grayscale JPEG stores one component rather than three, which a reader has to be told about
    /// or it shows the page in the wrong colours.
    /// </remarks>
    public static void WriteGrayJpeg(string path, int width, int height, int quality = 100)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawRect(new SKRect(0, 0, width / 2f, height), new SKPaint { Color = SKColors.Black });
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>Returns the orientation as an EXIF reader independent of this app's library sees it.</summary>
    /// <remarks>Used to check that software other than this app honors the written rotation.</remarks>
    public static SKEncodedOrigin ReadEncodedOrigin(string path)
    {
        using var codec = SKCodec.Create(path);
        return codec.EncodedOrigin;
    }

    /// <summary>Hash of the file bytes. Used to tell pages apart.</summary>
    public static string ContentHash(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>
    /// Hash of the decoded pixels. Used to judge whether image quality is preserved.
    /// </summary>
    /// <remarks>
    /// Rewriting EXIF tags changes the file bytes (EXIF segments are added or
    /// their length changes), so losslessness has to be judged from the
    /// decoded result rather than the bytes.
    /// </remarks>
    public static string PixelHash(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bitmap.GetPixelSpan()));
    }

    /// <summary>Whether the pixel leans red. Judged by channel ratio, allowing for JPEG compression error.</summary>
    public static bool IsRed(SKColor color) => color.Red > color.Blue;

    /// <summary>Whether the pixel leans blue.</summary>
    public static bool IsBlue(SKColor color) => color.Blue > color.Red;

    /// <summary>
    /// Whether the pixel can be considered almost black.
    /// </summary>
    /// <remarks>
    /// JPEG is lossy, so even a black dot is slightly mixed with the colors of neighboring pixels.
    /// Corner dot tests check for "sufficiently dark" rather than exact black.
    /// </remarks>
    public static bool IsDark(SKColor color, int threshold = 32) =>
        color.Red < threshold && color.Green < threshold && color.Blue < threshold;
}
