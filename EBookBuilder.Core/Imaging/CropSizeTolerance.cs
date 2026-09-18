namespace Lpubsppop01.EBookBuilder.Core.Imaging;

/// <summary>How much the dimensions of the cropping targets may differ and still be cropped together.</summary>
/// <remarks>
/// <para>
/// Cropping uses the margins chosen on a single preview image, so every target is expected to have
/// the same dimensions. Scanned pages are not exactly equal, however: the detection of the paper
/// edge and of the skew moves the boundary by a few pixels from page to page, and the pages
/// themselves are not cut to exactly the same size. Those pages are treated as the same size.
/// </para>
/// <para>
/// The allowed difference is the larger of a fixed number of pixels and a ratio of the length.
/// The variation coming from the scan grows with the resolution (the same skew is twice as many
/// pixels at twice the resolution), so a fixed number alone would be too strict for a high
/// resolution scan, while a ratio alone would be too strict for a small image.
/// </para>
/// </remarks>
public static class CropSizeTolerance
{
    /// <summary>The difference in pixels that is always allowed.</summary>
    public const int MinimumPixels = 4;

    /// <summary>The ratio of the length that is always allowed (1.5%).</summary>
    /// <remarks>
    /// The value is taken from real scans: the pages of one book came out with differences of
    /// around 0.9% (1446 and 1459 pixels wide), so it leaves room above that. What it has to keep
    /// apart is a page of a different format, which differs by tens of percent, so the exact value
    /// is not critical. If a scan needs more, raise this and check against the difference reported
    /// in the message (which is given in both pixels and percent).
    /// </remarks>
    public const double Ratio = 0.015;

    /// <summary>The allowed difference for a dimension of the given length.</summary>
    public static int AllowedDifference(int length) =>
        Math.Max(MinimumPixels, (int)Math.Ceiling(length * Ratio));

    /// <summary>Whether the two dimensions can be treated as the same size.</summary>
    /// <remarks>
    /// The comparison is made against a single reference rather than between neighbours, so that
    /// the difference cannot accumulate over a long run of pages.
    /// </remarks>
    public static bool AreSameSize(ImageSize reference, ImageSize size) =>
        Math.Abs(reference.Width - size.Width) <= AllowedDifference(reference.Width) &&
        Math.Abs(reference.Height - size.Height) <= AllowedDifference(reference.Height);

    /// <summary>The difference between the two sizes in pixels (the larger of the two dimensions).</summary>
    public static int Difference(ImageSize reference, ImageSize size) =>
        Math.Max(Math.Abs(reference.Width - size.Width), Math.Abs(reference.Height - size.Height));
}
