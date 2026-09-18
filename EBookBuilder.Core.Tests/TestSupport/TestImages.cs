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
