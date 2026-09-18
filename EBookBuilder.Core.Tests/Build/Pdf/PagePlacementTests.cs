using Lpubsppop01.EBookBuilder.Core.Build.Pdf;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build.Pdf;

/// <summary>
/// Verification of the page placement.
/// </summary>
/// <remarks>
/// The matrices below were confirmed against a renderer rather than derived on paper: a four
/// cornered image was placed with each of them and drawn by Ghostscript at 72 dpi, and the colours
/// were read back at the four corners of the result. Both a negated <c>d</c>, which turns the page
/// upside down, and a swap of <c>b</c> with <c>c</c>, which turns it the wrong way, are easy to
/// write and hard to notice, so the values are pinned here.
/// </remarks>
public class PagePlacementTests
{
    static readonly ImageSize CodedSize = new(20, 40);

    static double[] MatrixOf(PdfPagePlacement placement) =>
        [placement.A, placement.B, placement.C, placement.D, placement.E, placement.F];

    #region Matrices

    [Theory]
    // At 72 dpi a point is a pixel, so the page box is the image size.
    [InlineData(ExifOrientation.HorizontalNormal, 72, 20, 40, 20, 0, 0, 40, 0, 0)]
    [InlineData(ExifOrientation.Rotate180, 72, 20, 40, -20, 0, 0, -40, 20, 40)]
    [InlineData(ExifOrientation.Rotate90CW, 72, 40, 20, 0, -20, 40, 0, 0, 20)]
    [InlineData(ExifOrientation.Rotate270CW, 72, 40, 20, 0, 20, -40, 0, 40, 0)]
    // A quarter turn turns the page box on its side.
    [InlineData(ExifOrientation.HorizontalNormal, 300, 4.8, 9.6, 4.8, 0, 0, 9.6, 0, 0)]
    [InlineData(ExifOrientation.Rotate180, 300, 4.8, 9.6, -4.8, 0, 0, -9.6, 4.8, 9.6)]
    [InlineData(ExifOrientation.Rotate90CW, 300, 9.6, 4.8, 0, -4.8, 9.6, 0, 0, 4.8)]
    [InlineData(ExifOrientation.Rotate270CW, 300, 9.6, 4.8, 0, 4.8, -9.6, 0, 9.6, 0)]
    public void ThePlacementMatchesTheOrientation(
        ExifOrientation orientation,
        int dpi,
        double pageWidth,
        double pageHeight,
        double a,
        double b,
        double c,
        double d,
        double e,
        double f)
    {
        var placement = PdfPagePlacement.For(CodedSize, orientation, dpi);

        Assert.Equal(pageWidth, placement.PageWidth, 6);
        Assert.Equal(pageHeight, placement.PageHeight, 6);

        var expected = new[] { a, b, c, d, e, f };
        var actual = MatrixOf(placement);
        for (var i = 0; i < 6; ++i) Assert.Equal(expected[i], actual[i], 6);
    }

    #endregion

    #region Corner invariant

    /// <summary>
    /// The property the matrices exist for: each corner of the stored image has to land on a corner
    /// of the page, and on the one the orientation puts it at.
    /// </summary>
    /// <remarks>
    /// This checks the sense of the transformation rather than its numbers, so a matrix that is the
    /// wrong way round fails here even if it was written down consistently.
    /// </remarks>
    [Theory]
    [InlineData(ExifOrientation.HorizontalNormal, "TL", "TR", "BR", "BL")]
    [InlineData(ExifOrientation.Rotate180, "BR", "BL", "TL", "TR")]
    [InlineData(ExifOrientation.Rotate90CW, "TR", "BR", "BL", "TL")]
    [InlineData(ExifOrientation.Rotate270CW, "BL", "TL", "TR", "BR")]
    public void TheImageCornersLandOnThePageCorners(
        ExifOrientation orientation, string topLeft, string topRight, string bottomRight, string bottomLeft)
    {
        var placement = PdfPagePlacement.For(CodedSize, orientation, 300);

        // The unit square the image XObject is drawn through, with the image's first row at the top.
        var coded = new (double U, double V)[] { (0, 1), (1, 1), (1, 0), (0, 0) };

        var pageCorners = new Dictionary<string, (double X, double Y)>
        {
            ["TL"] = (0, placement.PageHeight),
            ["TR"] = (placement.PageWidth, placement.PageHeight),
            ["BR"] = (placement.PageWidth, 0),
            ["BL"] = (0, 0),
        };

        var expected = new[] { topLeft, topRight, bottomRight, bottomLeft };

        for (var i = 0; i < coded.Length; ++i)
        {
            var x = placement.A * coded[i].U + placement.C * coded[i].V + placement.E;
            var y = placement.B * coded[i].U + placement.D * coded[i].V + placement.F;

            var corner = pageCorners[expected[i]];
            Assert.Equal(corner.X, x, 6);
            Assert.Equal(corner.Y, y, 6);
        }
    }

    /// <summary>
    /// Whatever the orientation, the image fills its page box exactly. The page placement must
    /// neither leave a margin nor crop, because the size the page is given is meant to be the size
    /// of the image.
    /// </summary>
    [Theory]
    [InlineData(ExifOrientation.HorizontalNormal)]
    [InlineData(ExifOrientation.Rotate180)]
    [InlineData(ExifOrientation.Rotate90CW)]
    [InlineData(ExifOrientation.Rotate270CW)]
    public void TheImageFillsItsPageBox(ExifOrientation orientation)
    {
        var placement = PdfPagePlacement.For(CodedSize, orientation, 300);

        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var (u, v) in new (double, double)[] { (0, 1), (1, 1), (1, 0), (0, 0) })
        {
            xs.Add(placement.A * u + placement.C * v + placement.E);
            ys.Add(placement.B * u + placement.D * v + placement.F);
        }

        Assert.Equal(0, xs.Min(), 6);
        Assert.Equal(placement.PageWidth, xs.Max(), 6);
        Assert.Equal(0, ys.Min(), 6);
        Assert.Equal(placement.PageHeight, ys.Max(), 6);
    }

    #endregion

    #region Content stream

    [Fact]
    public void TheContentStreamDrawsTheImageThroughTheMatrix()
    {
        var placement = PdfPagePlacement.For(CodedSize, ExifOrientation.Rotate90CW, 72);
        Assert.Equal("q 0 -20 40 0 0 20 cm /Im0 Do Q", placement.ToContentStream());
    }

    /// <summary>The numbers in the content stream are subject to the same culture rule as the rest.</summary>
    [Fact]
    public void TheContentStreamIsWrittenUnderAnyCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var placement = PdfPagePlacement.For(CodedSize, ExifOrientation.HorizontalNormal, 300);
            Assert.Equal("q 4.8 0 0 9.6 0 0 cm /Im0 Do Q", placement.ToContentStream());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    #endregion
}
