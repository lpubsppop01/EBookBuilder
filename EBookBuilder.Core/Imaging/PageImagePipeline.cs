using Lpubsppop01.EBookBuilder.Core.Exif;
using SkiaSharp;

namespace Lpubsppop01.EBookBuilder.Core.Imaging;

/// <summary>Image format to write out.</summary>
public enum OutputImageFormat
{
    /// <summary>JPEG (the quality parameter takes effect).</summary>
    Jpeg,

    /// <summary>PNG (lossless. The quality parameter is ignored).</summary>
    Png,
}

/// <summary>
/// Decoding, rotation, scaling, cropping, corner dot drawing and encoding of page images.
/// </summary>
/// <remarks>
/// <para>
/// A replacement for the original WPF version's <c>MyBitmapImageUtility</c>. WPF's
/// <c>BitmapImage.Rotation</c> / <c>DecodePixelWidth</c> / <c>JpegBitmapEncoder</c> are each
/// replaced with the corresponding SkiaSharp API.
/// </para>
/// <para>
/// Rotating by rewriting the EXIF tag is this app's principle, so rotating pixels here is
/// limited to the case where the decoded result is written out
/// (in that case the tag has to be normalized to Normal).
/// </para>
/// </remarks>
public static class PageImagePipeline
{
    /// <summary>Length of one side of a corner dot (in pixels).</summary>
    public const int CornerDotSize = 2;

    /// <summary>
    /// Sampling options used when drawing at the same scale (no interpolation, i.e. pixels are
    /// copied verbatim).
    /// </summary>
    static readonly SKSamplingOptions ExactCopySampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>Default quality for JPEG re-encoding.</summary>
    /// <remarks>
    /// The original was fixed at <c>JpegBitmapEncoder</c>'s default of 75 and did not expose it
    /// in the UI. That was a cause of unnoticed quality loss, so the default is raised and the
    /// value is now configurable.
    /// </remarks>
    public const int DefaultJpegQuality = 90;

    /// <summary>
    /// Reads only the dimensions without decoding (the raw on-file dimensions, without applying
    /// the EXIF rotation).
    /// </summary>
    public static ImageSize ReadEncodedSize(string path)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot load as an image: {path}");
        return new ImageSize(codec.Info.Width, codec.Info.Height);
    }

    /// <summary>Returns the dimensions after the EXIF rotation is applied.</summary>
    public static ImageSize ReadOrientedSize(string path)
    {
        var encoded = ReadEncodedSize(path);
        return ExifOrientationStore.Read(path).SwapsWidthAndHeight() ? encoded.Swapped : encoded;
    }

    /// <summary>
    /// Computes the dimensions that fit within <paramref name="target"/> while preserving the
    /// aspect ratio (aspect-fit).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The output is the fitted dimensions themselves, not dimensions that fill the target frame.
    /// It neither pads with margins (letterboxing) nor crops the overflow.
    /// </para>
    /// <para>
    /// When the result would be larger than the source image, the source dimensions are returned
    /// (no upscaling). This is a deliberate difference from the original WPF version, which
    /// upscaled when the target frame was larger. For the purpose of shrinking scanned images
    /// for e-books, upscaling needlessly degrades both quality and file size, so upscaling is
    /// forbidden in this port.
    /// </para>
    /// </remarks>
    public static ImageSize FitSize(ImageSize source, ImageSize target)
    {
        if (source.Width <= 0 || source.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(source), source, "The source image dimensions are invalid.");
        if (target.Width <= 0 || target.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(target), target, "The target dimensions are invalid.");

        var sourceAspect = (double)source.Width / source.Height;
        var targetAspect = (double)target.Width / target.Height;

        int width, height;
        if (sourceAspect > targetAspect)
        {
            // The source image is wider. Width is the constraint.
            width = target.Width;
            height = (int)(target.Width / sourceAspect);
        }
        else
        {
            // The source image is taller (or has the same ratio). Height is the constraint.
            height = target.Height;
            width = (int)(target.Height * sourceAspect);
        }

        if (width > source.Width || height > source.Height) return source;

        return new ImageSize(Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>
    /// Decodes an image and returns it with the EXIF rotation applied.
    /// </summary>
    /// <param name="path">Input file.</param>
    /// <param name="fitWithin">
    /// When specified, the image is decoded shrunk to fit within this frame.
    /// JPEG's DCT scaled decoding is used to keep down the memory and time needed when handling
    /// large scanned images.
    /// </param>
    /// <remarks>The caller is responsible for disposing the returned <see cref="SKBitmap"/>.</remarks>
    public static SKBitmap DecodeOriented(string path, ImageSize? fitWithin = null)
    {
        var orientation = ExifOrientationStore.Read(path);
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot load as an image: {path}");

        var encoded = new ImageSize(codec.Info.Width, codec.Info.Height);
        var oriented = orientation.SwapsWidthAndHeight() ? encoded.Swapped : encoded;

        // The target dimensions are determined in the "after rotation" coordinate system, so
        // convert them back to the original coordinate system for decoding.
        var desired = encoded;
        if (fitWithin is { } target)
        {
            var fitted = FitSize(oriented, target);
            desired = orientation.SwapsWidthAndHeight() ? fitted.Swapped : fitted;
        }

        var decoded = DecodeScaled(codec, encoded, desired);
        return ApplyOrientation(decoded, orientation);
    }

    /// <summary>
    /// Decodes at the DCT scale closest to the target dimensions, then matches the rest with a
    /// high quality resample if needed.
    /// </summary>
    static SKBitmap DecodeScaled(SKCodec codec, ImageSize encoded, ImageSize desired)
    {
        var scale = Math.Min(
            (float)desired.Width / encoded.Width,
            (float)desired.Height / encoded.Height);
        scale = Math.Min(scale, 1f);

        // On the codec side, JPEG supports only discrete downscales such as 1/2, 1/4 and 1/8.
        // The dimensions actually obtainable are queried, and any shortfall is resampled later.
        var scaled = scale >= 1f ? new SKSizeI(encoded.Width, encoded.Height) : codec.GetScaledDimensions(scale);

        var info = new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Failed to decode the image ({result}).");
        }

        if (scaled.Width == desired.Width && scaled.Height == desired.Height) return bitmap;

        var resizedInfo = new SKImageInfo(desired.Width, desired.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var resized = bitmap.Resize(resizedInfo, new SKSamplingOptions(SKCubicResampler.Mitchell));
        bitmap.Dispose();
        return resized ?? throw new InvalidDataException("Failed to resize the image.");
    }

    /// <summary>
    /// Applies the EXIF rotation to a decoded image.
    /// </summary>
    /// <remarks>
    /// When there is no rotation, the input is returned as-is. For anything other than
    /// <see cref="ExifOrientation.HorizontalNormal"/>, the source bitmap is disposed and a new
    /// bitmap is returned.
    /// </remarks>
    static SKBitmap ApplyOrientation(SKBitmap source, ExifOrientation orientation)
    {
        if (orientation == ExifOrientation.HorizontalNormal) return source;

        var (width, height) = (source.Width, source.Height);
        var target = orientation.SwapsWidthAndHeight()
            ? new ImageSize(height, width)
            : new ImageSize(width, height);

        var result = new SKBitmap(new SKImageInfo(target.Width, target.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(result))
        {
            switch (orientation)
            {
                case ExifOrientation.Rotate90CW:
                    canvas.Translate(height, 0);
                    canvas.RotateDegrees(90);
                    break;
                case ExifOrientation.Rotate180:
                    canvas.Translate(width, height);
                    canvas.RotateDegrees(180);
                    break;
                case ExifOrientation.Rotate270CW:
                    canvas.Translate(0, width);
                    canvas.RotateDegrees(270);
                    break;
            }
            canvas.DrawBitmap(source, 0, 0, ExactCopySampling);
        }

        source.Dispose();
        return result;
    }

    /// <summary>
    /// Crops to the specified rectangle. The coordinates are interpreted in the coordinate
    /// system after the EXIF rotation is applied.
    /// </summary>
    /// <remarks>The caller is responsible for disposing the returned <see cref="SKBitmap"/>.</remarks>
    public static SKBitmap Crop(SKBitmap source, int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "The crop size is invalid.");
        if (left < 0 || top < 0 || left + width > source.Width || top + height > source.Height)
            throw new ArgumentOutOfRangeException(nameof(left), "The crop area extends outside the image.");

        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(result))
        {
            canvas.Translate(-left, -top);
            canvas.DrawBitmap(source, 0, 0, ExactCopySampling);
        }
        return result;
    }

    /// <summary>
    /// Draws black dots at the four corners (to visually check that the output pages are cropped
    /// correctly).
    /// </summary>
    /// <remarks>
    /// As in the original, 2x2 pixel black blocks are placed at the four corners.
    /// The original hard-coded the channel order of the pixel array as BGRA, but here everything
    /// is always handled as BGRA8888, so no mix-up can occur.
    /// </remarks>
    public static void DrawCornerDots(SKBitmap bitmap)
    {
        var dots = CornerDotSize;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var yIsInTarget = y < dots || y >= bitmap.Height - dots;
            if (!yIsInTarget) continue;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var xIsInTarget = x < dots || x >= bitmap.Width - dots;
                if (xIsInTarget) bitmap.SetPixel(x, y, SKColors.Black);
            }
        }
    }

    /// <summary>Writes an image out to a file. An existing file is overwritten.</summary>
    public static void Encode(SKBitmap bitmap, string path, OutputImageFormat format, int quality)
    {
        if (format == OutputImageFormat.Jpeg && quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "JPEG quality must be between 1 and 100.");

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            format == OutputImageFormat.Jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,
            quality);
        if (data is null) throw new InvalidDataException($"Failed to encode the image: {path}");

        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>Determines the output format from the extension.</summary>
    public static OutputImageFormat FormatFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => OutputImageFormat.Jpeg,
        ".png" => OutputImageFormat.Png,
        _ => throw new InvalidDataException($"Unsupported image format: {path}"),
    };
}
