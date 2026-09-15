using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Pages;

/// <summary>Verification of the serial number rules. The digit count is determined by the page count.</summary>
public class PageNamingTests
{
    [Theory]
    [InlineData(1, "{0:0}.jpg")]
    [InlineData(2, "{0:0}.jpg")]
    [InlineData(9, "{0:0}.jpg")]
    [InlineData(10, "{0:00}.jpg")]
    [InlineData(99, "{0:00}.jpg")]
    [InlineData(100, "{0:000}.jpg")]
    [InlineData(999, "{0:000}.jpg")]
    [InlineData(1000, "{0:0000}.jpg")]
    public void DigitCountIsDeterminedByThePageCount(int count, string expected)
    {
        Assert.Equal(expected, PageNaming.BuildFilenameFormat(count));
    }

    [Theory]
    [InlineData(0, 1, "0.jpg")]
    [InlineData(0, 10, "00.jpg")]
    [InlineData(9, 10, "09.jpg")]
    [InlineData(0, 100, "000.jpg")]
    [InlineData(99, 100, "099.jpg")]
    public void FilenameIsBuiltToMatchThePageCount(int index, int count, string expected)
    {
        Assert.Equal(expected, PageNaming.FormatFilename(index, count));
    }

    [Fact]
    public void EmptyListIsTreatedAsSerialNumbers()
    {
        Assert.True(PageNaming.AreSerialNumbers([]));
    }

    [Fact]
    public void ValidSerialNumbersReturnTrue()
    {
        Assert.True(PageNaming.AreSerialNumbers(["0.jpg"]));
        Assert.True(PageNaming.AreSerialNumbers(["0.jpg", "1.jpg"]));
        Assert.True(PageNaming.AreSerialNumbers(["0.jpg", "1.jpg", "2.jpg"]));
        // Two digits only start at 10 items
        Assert.True(PageNaming.AreSerialNumbers(Enumerable.Range(0, 10).Select(i => $"{i:00}.jpg").ToArray()));
    }

    [Fact]
    public void GapsReturnFalse()
    {
        Assert.False(PageNaming.AreSerialNumbers(["0.jpg", "2.jpg"]));
    }

    [Fact]
    public void WrongOrderReturnsFalse()
    {
        Assert.False(PageNaming.AreSerialNumbers(["1.jpg", "0.jpg"]));
    }

    [Fact]
    public void WrongDigitCountReturnsFalse()
    {
        // With 2 items, "0.jpg" / "1.jpg" are correct and "00.jpg" has an extra digit
        Assert.False(PageNaming.AreSerialNumbers(["00.jpg", "01.jpg"]));
    }

    [Fact]
    public void DifferentExtensionReturnsFalse()
    {
        Assert.False(PageNaming.AreSerialNumbers(["0.jpeg", "1.jpeg"]));
    }

    [Theory]
    [InlineData(-1)]
    public void NegativeCountThrows(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PageNaming.BuildFilenameFormat(count));
    }
}
