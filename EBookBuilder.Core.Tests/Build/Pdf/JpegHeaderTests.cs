using Lpubsppop01.EBookBuilder.Core.Build.Pdf;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Build.Pdf;

/// <summary>Verification of the JPEG frame header reading.</summary>
/// <remarks>
/// The component count decides the colour space a page is declared with, so a wrong answer shows
/// up as a page that renders in the wrong colours rather than as a failure to open.
/// </remarks>
public class JpegHeaderTests
{
    /// <summary>Builds a JPEG carrying the markers that lead up to a frame header.</summary>
    /// <param name="componentCount">The component count to declare.</param>
    /// <param name="frameMarker">The frame marker to use, which varies with the coding process.</param>
    /// <param name="app1PayloadLength">Length of an APP1 segment placed before the frame, standing in for an EXIF thumbnail.</param>
    /// <param name="fillBytes">Fill bytes inserted before the frame marker.</param>
    static byte[] BuildJpeg(
        int componentCount,
        byte frameMarker = 0xC0,
        int app1PayloadLength = 0,
        int fillBytes = 0)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (app1PayloadLength > 0)
        {
            var app1Length = app1PayloadLength + 2;
            bytes.AddRange([0xFF, 0xE1, (byte)(app1Length >> 8), (byte)app1Length]);
            bytes.AddRange(new byte[app1PayloadLength]);
        }

        for (var i = 0; i < fillBytes; ++i) bytes.Add(0xFF);

        var frameLength = 8 + 3 * componentCount;
        bytes.AddRange([0xFF, frameMarker, (byte)(frameLength >> 8), (byte)frameLength]);
        bytes.Add(8);                       // sample precision
        bytes.AddRange([0x01, 0x2C]);       // height, 300
        bytes.AddRange([0x00, 0xC8]);       // width, 200
        bytes.Add((byte)componentCount);
        bytes.AddRange(new byte[3 * componentCount]);

        bytes.AddRange([0xFF, 0xD9]);
        return bytes.ToArray();
    }

    static int CountOf(byte[] bytes)
    {
        using var file = TempFile.WithBytes(bytes, "page.jpg");
        return JpegHeader.ReadComponentCount(file.Path);
    }

    [Fact]
    public void GrayscaleIsOneComponent() => Assert.Equal(1, CountOf(BuildJpeg(1)));

    [Fact]
    public void YCbCrIsThreeComponents() => Assert.Equal(3, CountOf(BuildJpeg(3)));

    [Fact]
    public void CmykIsFourComponents() => Assert.Equal(4, CountOf(BuildJpeg(4)));

    /// <summary>
    /// A progressive JPEG uses a different frame marker, and only the markers in the 0xC0-0xCF
    /// range that really are frames may be taken for one.
    /// </summary>
    [Theory]
    [InlineData(0xC0)]
    [InlineData(0xC1)]
    [InlineData(0xC2)]
    [InlineData(0xCF)]
    public void EveryFrameMarkerIsRecognised(byte marker) => Assert.Equal(3, CountOf(BuildJpeg(3, marker)));

    /// <summary>
    /// A Huffman table starts with 0xC4, which sits inside the frame marker range but is not a frame.
    /// </summary>
    [Fact]
    public void AHuffmanTableIsNotTakenForAFrame()
    {
        var bytes = BuildJpeg(3);
        // Replace the frame marker with a Huffman table marker of the same shape.
        bytes[2] = 0xC4;
        Assert.Equal(0, CountOf(bytes));
    }

    /// <summary>
    /// Real scans carry an EXIF thumbnail in their APP1 segment, so the frame header can be tens of
    /// kilobytes in. A parser that only reads a fixed prefix fails on them. The length field is two
    /// bytes, so this is near the largest a single segment can be.
    /// </summary>
    [Fact]
    public void TheFrameIsFoundPastALargeExifSegment() =>
        Assert.Equal(3, CountOf(BuildJpeg(3, app1PayloadLength: 65000)));

    /// <summary>Padding bytes may be inserted before a marker, and must not be mistaken for one.</summary>
    [Fact]
    public void FillBytesBeforeAMarkerAreSkipped() =>
        Assert.Equal(3, CountOf(BuildJpeg(3, fillBytes: 3)));

    [Fact]
    public void ATruncatedFileReadsAsUnknown()
    {
        var bytes = BuildJpeg(3);
        Assert.Equal(0, CountOf(bytes[..8]));
    }

    [Fact]
    public void ANonJpegReadsAsUnknown() =>
        Assert.Equal(0, CountOf([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00]));

    #region Embeddability

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void OnlyOneAndThreeComponentsCanBeStoredAsTheyAre(int componentCount, bool expected)
    {
        using var file = TempFile.WithBytes(BuildJpeg(componentCount), "page.jpg");
        Assert.Equal(expected, JpegHeader.IsEmbeddable(file.Path));
    }

    #endregion
}
