using Lpubsppop01.EBookBuilder.Core.Pages;
using Lpubsppop01.EBookBuilder.Core.Tests.TestSupport;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Pages;

/// <summary>Verification of folder scanning. The sort order in particular is prone to platform-dependent bugs.</summary>
public class PageFolderTests
{
    [Fact]
    public void SerialPagesAreReturnedInOrdinalOrder()
    {
        using var folder = TempPageFolder.CreateWithPages("00.jpg", "01.jpg", "02.jpg");
        Assert.Equal(["00.jpg", "01.jpg", "02.jpg"], folder.EnumerateFilenames());
    }

    [Fact]
    public void PagesAreSortedByNameIndependentlyOfFileSystemOrder()
    {
        // The original used the enumeration order as-is, so it implicitly depended on NTFS
        // returning entries in name order. On ext4 the order is unspecified,
        // so the code sorts explicitly here.
        // The creation order is deliberately different from the name order, to confirm that the sorting takes effect.
        using var folder = TempPageFolder.CreateWithPages("10.jpg", "2.jpg", "1.jpg", "9.jpg");

        Assert.Equal(["1.jpg", "10.jpg", "2.jpg", "9.jpg"], folder.EnumerateFilenames());
    }

    [Fact]
    public void ZeroPaddedSerialNamesAreSortedCorrectly()
    {
        using var folder = TempPageFolder.CreateWithPages("009.jpg", "010.jpg", "001.jpg");

        Assert.Equal(["001.jpg", "009.jpg", "010.jpg"], folder.EnumerateFilenames());
    }

    [Fact]
    public void NonJpegFilesAreIgnored()
    {
        using var folder = TempPageFolder.CreateWithPages("00.jpg", "01.jpg");
        TestImages.WriteTwoToneJpeg(folder.FilePath("notes.txt"), 10, 10);
        TestImages.WriteTwoToneJpeg(folder.FilePath("cover.png"), 10, 10);

        Assert.Equal(["00.jpg", "01.jpg"], folder.EnumerateFilenames());
    }

    [Fact]
    public void ExtensionCaseIsIgnored()
    {
        using var folder = TempPageFolder.CreateWithPages("00.JPG", "01.JPEG", "02.jpg");

        var actual = folder.EnumerateFilenames();
        Assert.Equal(3, actual.Count);
        Assert.Contains("00.JPG", actual);
        Assert.Contains("01.JPEG", actual);
    }

    [Fact]
    public void MissingFolderReturnsEmpty()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Empty(PageFolder.EnumeratePageFilenames(missing));
        Assert.False(PageFolder.Exists(missing));
    }

    [Fact]
    public void ExistingFolderIsDetectedCorrectly()
    {
        using var folder = TempPageFolder.CreateEmpty();
        Assert.True(PageFolder.Exists(folder.Path));
    }
}
