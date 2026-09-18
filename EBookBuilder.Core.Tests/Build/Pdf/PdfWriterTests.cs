using System.Globalization;
using System.Text;
using Lpubsppop01.EBookBuilder.Core.Build.Pdf;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build.Pdf;

/// <summary>Verification of the PDF byte writing.</summary>
public class PdfWriterTests
{
    /// <summary>
    /// A payload holding the very byte sequences a naive parser would stop at, so that cutting the
    /// stream out by searching for them is caught.
    /// </summary>
    static readonly byte[] TrickyPayload =
        Encoding.ASCII.GetBytes("start endstream endobj xref trailer 12345 endstream endobj");

    /// <summary>Writes a small but complete PDF.</summary>
    static byte[] BuildPdf(byte[]? payload = null)
    {
        using var buffer = new MemoryStream();
        var writer = new PdfWriter(buffer);

        writer.WriteHeader();
        var catalogId = writer.AllocateObjectId();
        var pagesId = writer.AllocateObjectId();
        var imageId = writer.AllocateObjectId();
        var contentId = writer.AllocateObjectId();

        writer.WriteObject(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");
        writer.WriteObject(
            pagesId,
            $"<< /Type /Pages /Kids [ {contentId} 0 R ] /Count 1 >>");
        writer.WriteStreamObject(
            imageId,
            $"<< /Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray "
            + $"/BitsPerComponent 8 /Filter /DCTDecode",
            payload ?? [0xFF, 0xD8, 0xFF, 0xD9]);
        writer.WriteStreamObject(contentId, "", Encoding.ASCII.GetBytes("q 1 0 0 1 0 0 cm /Im0 Do Q"));

        writer.WriteXrefAndTrailer(catalogId);
        return buffer.ToArray();
    }

    [Fact]
    public void TheFileStartsWithTheHeaderAndABinaryComment()
    {
        var pdf = PdfInspector.Parse(BuildPdf());

        Assert.StartsWith("%PDF-1.4\n%âãÏÓ\n", pdf.Text);
    }

    [Fact]
    public void TheFileEndsWithEof() => Assert.EndsWith("%%EOF\n", PdfInspector.Parse(BuildPdf()).Text);

    /// <summary>
    /// The structural check: every entry in the cross-reference table has to point at its own object.
    /// Getting this wrong is the classic way to produce a file that will not open.
    /// </summary>
    [Fact]
    public void XrefOffsetsPointAtTheirObjects()
    {
        var pdf = PdfInspector.Parse(BuildPdf());
        var offsets = pdf.XrefOffsets;

        Assert.Equal(pdf.Objects.Count, offsets.Count);
        for (var i = 0; i < offsets.Count; ++i)
            Assert.StartsWith($"{pdf.Objects[i].Id} 0 obj\n", pdf.TextAt(offsets[i], 16));
    }

    [Fact]
    public void StartxrefPointsAtTheXrefTable()
    {
        var pdf = PdfInspector.Parse(BuildPdf());
        Assert.StartsWith("xref\n", pdf.TextAt(pdf.StartXrefOffset, 5));
    }

    /// <summary>
    /// A stream is cut out with the declared length, and the declaration comes from the payload, so
    /// a payload containing endstream or endobj byte sequences survives intact.
    /// </summary>
    [Fact]
    public void APayloadHoldingMarkerTextSurvivesIntact()
    {
        var pdf = PdfInspector.Parse(BuildPdf(TrickyPayload));
        Assert.Equal(TrickyPayload, pdf.Images.Single().Payload);
    }

    [Fact]
    public void TheDeclaredLengthMatchesThePayload()
    {
        var pdf = PdfInspector.Parse(BuildPdf(TrickyPayload));
        var image = pdf.Images.Single();

        Assert.Equal(TrickyPayload.Length, image.Payload!.Length);
        Assert.Contains($"/Length {TrickyPayload.Length} ", image.Dictionary, StringComparison.Ordinal);
    }

    #region Guards

    /// <summary>
    /// A missing object leaves a hole in the cross-reference table, so it is turned into a failure
    /// at the point it can still be reported rather than a file that will not open.
    /// </summary>
    [Fact]
    public void AnAllocatedObjectMustBeWritten()
    {
        using var buffer = new MemoryStream();
        var writer = new PdfWriter(buffer);
        writer.WriteHeader();
        var catalogId = writer.AllocateObjectId();
        writer.AllocateObjectId();
        writer.WriteObject(catalogId, "<< >>");

        Assert.Throws<InvalidOperationException>(() => writer.WriteXrefAndTrailer(catalogId));
    }

    [Fact]
    public void AnObjectCannotBeWrittenTwice()
    {
        using var buffer = new MemoryStream();
        var writer = new PdfWriter(buffer);
        var id = writer.AllocateObjectId();
        writer.WriteObject(id, "<< >>");

        Assert.Throws<InvalidOperationException>(() => writer.WriteObject(id, "<< >>"));
    }

    [Fact]
    public void AnUnallocatedObjectNumberIsRejected()
    {
        using var buffer = new MemoryStream();
        var writer = new PdfWriter(buffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteObject(1, "<< >>"));
    }

    #endregion

    #region Culture

    /// <summary>
    /// The number format is not cosmetic: a culture that writes 0,24 would put a comma into the
    /// content stream and break the file. Only the command line sets InvariantGlobalization, so
    /// the desktop application really does run under the user's own culture.
    /// </summary>
    [Fact]
    public void NumbersUseTheInvariantCultureWhateverTheMachineCultureIs()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("0.24", PdfWriter.Format(0.24));
            Assert.Equal("-0.24", PdfWriter.Format(-0.24));
            Assert.Equal("384", PdfWriter.Format(384));
            Assert.Equal("28.8", PdfWriter.Format(28.8));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>The cross-reference offsets are numbers too, and are written under the same rule.</summary>
    [Fact]
    public void TheCrossReferenceTableIsWrittenUnderAnyCulture()
    {
        var original = CultureInfo.CurrentCulture;
        byte[] bytes;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            bytes = BuildPdf();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        var pdf = PdfInspector.Parse(bytes);
        Assert.Equal(pdf.Objects.Count, pdf.XrefOffsets.Count);
        Assert.StartsWith("%PDF-1.4", pdf.Text);
    }

    #endregion
}
