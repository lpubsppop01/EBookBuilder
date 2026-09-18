using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Build.Pdf;

/// <summary>Where and how one page's image is placed on its PDF page.</summary>
/// <remarks>
/// <para>
/// A PDF page has no orientation tag, so the EXIF rotation has to be expressed some other way. It
/// is expressed here as the placement of the image: the page box is given the rotated dimensions
/// and the content stream carries the rotation as a transformation matrix. The JPEG bytes
/// themselves are left alone, which is what keeps a rotated page from being re-encoded.
/// </para>
/// <para>
/// The alternative would be the page's <c>/Rotate</c> entry, but then the page box would describe
/// the unrotated page and printing, thumbnails and fit-to-page would all have to apply the
/// rotation themselves. Emitting both is never correct: the page would be rotated twice.
/// </para>
/// </remarks>
/// <param name="PageWidth">Width of the page box, in points.</param>
/// <param name="PageHeight">Height of the page box, in points.</param>
/// <param name="A">The matrix's <c>a</c>, scaling the unit square's x axis onto the page's x axis.</param>
/// <param name="B">The matrix's <c>b</c>.</param>
/// <param name="C">The matrix's <c>c</c>.</param>
/// <param name="D">The matrix's <c>d</c>.</param>
/// <param name="E">The matrix's <c>e</c>, the translation along x.</param>
/// <param name="F">The matrix's <c>f</c>, the translation along y.</param>
public readonly record struct PdfPagePlacement(
    double PageWidth,
    double PageHeight,
    double A,
    double B,
    double C,
    double D,
    double E,
    double F)
{
    /// <summary>The name the image is registered under in the page's resources.</summary>
    public const string ResourceName = "Im0";

    /// <summary>Builds the placement of an image on its page.</summary>
    /// <param name="codedSize">The image's own pixel dimensions, before the EXIF rotation.</param>
    /// <param name="orientation">The EXIF rotation to reproduce.</param>
    /// <param name="dpi">Resolution converting pixels into points.</param>
    /// <remarks>
    /// An image XObject is drawn by mapping its unit square onto the page, and the image's first
    /// row sits at the top of that square. So an unrotated image needs the plainly positive matrix
    /// <c>w 0 0 h 0 0</c>; negating <c>d</c> would turn the page upside down.
    /// </remarks>
    public static PdfPagePlacement For(ImageSize codedSize, ExifOrientation orientation, int dpi)
    {
        var k = 72.0 / dpi;
        var w = codedSize.Width * k;
        var h = codedSize.Height * k;

        // Turning the image a quarter turn turns the page box on its side as well.
        return orientation switch
        {
            ExifOrientation.Rotate180 =>
                new PdfPagePlacement(w, h, -w, 0, 0, -h, w, h),
            ExifOrientation.Rotate90CW =>
                new PdfPagePlacement(h, w, 0, -w, h, 0, 0, w),
            ExifOrientation.Rotate270CW =>
                new PdfPagePlacement(h, w, 0, w, -h, 0, h, 0),
            _ =>
                new PdfPagePlacement(w, h, w, 0, 0, h, 0, 0),
        };
    }

    /// <summary>The content stream that draws the image onto its page.</summary>
    public string ToContentStream()
    {
        var matrix = string.Join(' ',
            PdfWriter.Format(A), PdfWriter.Format(B), PdfWriter.Format(C),
            PdfWriter.Format(D), PdfWriter.Format(E), PdfWriter.Format(F));
        return $"q {matrix} cm /{ResourceName} Do Q";
    }
}
