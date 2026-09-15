using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.App.Imaging;

/// <summary>Converts SkiaSharp images into a form Avalonia can display.</summary>
/// <remarks>
/// <para>
/// <see cref="PageImagePipeline"/> handles loading and rotating images.
/// Avalonia's <c>Bitmap</c> does not apply the EXIF orientation, so images for display
/// are converted after going through the same path.
/// </para>
/// <para>
/// The conversion copies the pixels as they are instead of re-encoding to PNG or the like.
/// The preview is rebuilt every time a page is selected, so speed matters here.
/// </para>
/// </remarks>
public static class BitmapInterop
{
    /// <summary>Copies the pixels as they are to create a bitmap for display.</summary>
    public static WriteableBitmap ToAvalonia(SKBitmap source)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(source.Width, source.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var buffer = bitmap.Lock();

        // A row of an SKBitmap can be padded, so copy only the width's worth for each row.
        var rowLength = source.Width * 4;
        var pixels = source.GetPixelSpan();

        for (var y = 0; y < source.Height; ++y)
        {
            var row = pixels.Slice(y * source.RowBytes, rowLength).ToArray();
            Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), rowLength);
        }

        return bitmap;
    }

    /// <summary>
    /// Loads an image for preview at a size that fits within the specified bounds.
    /// </summary>
    /// <remarks>Returns it with the EXIF orientation applied. Null if it cannot be read.</remarks>
    public static WriteableBitmap? LoadPreview(string path, ImageSize maxSize)
    {
        try
        {
            using var decoded = PageImagePipeline.DecodeOriented(path, maxSize);
            return ToAvalonia(decoded);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            return null;
        }
    }
}
