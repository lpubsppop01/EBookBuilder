using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Pages;

/// <summary>
/// Verification of the comparison that decides the page order.
/// </summary>
/// <remarks>
/// A digit run has to compare exactly without ever being read into a number, so the boundary cases
/// (runs longer than any integer type, runs of the same value written with a different number of
/// leading zeros) are written out here directly, rather than through a folder of files.
/// </remarks>
public class NaturalFilenameComparerTests
{
    [Theory]
    [InlineData("1.jpg", "2.jpg")]
    [InlineData("9.jpg", "10.jpg")]
    [InlineData("10.jpg", "11.jpg")]
    [InlineData("99.jpg", "100.jpg")]
    [InlineData("page - 2.jpg", "page - 10.jpg")]
    [InlineData("page - 10.jpg", "page - 100.jpg")]
    [InlineData("page - 99.jpg", "page - 101.jpg")]
    public void DigitRunsAreComparedAsNumbers(string first, string second) => AssertComesFirst(first, second);

    [Theory]
    [InlineData("9223372036854775807.jpg", "9223372036854775808.jpg")]   // the second is past long.MaxValue
    [InlineData("18446744073709551615.jpg", "18446744073709551616.jpg")] // the second is past ulong.MaxValue
    [InlineData("999999999999999999999999999999.jpg", "1000000000000000000000000000000.jpg")]
    public void DigitRunsLongerThanAnyIntegerTypeStillCompare(string first, string second) =>
        AssertComesFirst(first, second);

    [Theory]
    [InlineData("1.jpg", "01.jpg")]
    [InlineData("01.jpg", "001.jpg")]
    [InlineData("0.jpg", "00.jpg")]
    [InlineData("10.jpg", "010.jpg")]
    public void TheSameNumberWrittenWithFewerLeadingZerosComesFirst(string first, string second) =>
        AssertComesFirst(first, second);

    [Theory]
    [InlineData("ch1 - 2.jpg", "ch1 - 10.jpg")]
    [InlineData("ch2 - 1.jpg", "ch10 - 1.jpg")]
    [InlineData("v1p2.jpg", "v1p10.jpg")]
    [InlineData("v1p9.jpg", "v2p1.jpg")]
    public void EveryDigitRunInANameIsComparedAsANumber(string first, string second) =>
        AssertComesFirst(first, second);

    [Theory]
    [InlineData("contents.jpg", "cover.jpg")]
    [InlineData("2.jpg", "cover.jpg")]
    [InlineData("page.jpg", "page2.jpg")]
    [InlineData("1.jpg", "1a.jpg")]
    public void EverythingApartFromDigitRunsIsComparedAsText(string first, string second) =>
        AssertComesFirst(first, second);

    [Theory]
    [InlineData("A.jpg", "B.jpg")]
    [InlineData("B.jpg", "a.jpg")]
    public void TextIsComparedByCodeUnitSoTheOrderDoesNotFollowTheLocale(string first, string second) =>
        AssertComesFirst(first, second);

    [Fact]
    public void SerialNumbersKeepTheOrderTheyHadUnderTheOrdinalComparison()
    {
        var names = new[] { "002.jpg", "010.jpg", "000.jpg", "001.jpg" };

        Assert.Equal(
            ["000.jpg", "001.jpg", "002.jpg", "010.jpg"],
            names.OrderBy(name => name, NaturalFilenameComparer.Instance));
    }

    [Fact]
    public void TheNamesFromTheReportedFolderAreOrderedNumerically()
    {
        // The reported case: 102 pages whose names differ only in a number that is not zero padded.
        // The ordinal comparison put these in the order 1, 10, 100, 101, 102, 11, … .
        const string prefix = "ページ - ";
        var names = new[] { "1", "2", "10", "11", "100", "101", "102" }.Select(number => $"{prefix}{number}.jpg");
        var shuffled = new[] { "10", "100", "2", "1", "102", "11", "101" }.Select(number => $"{prefix}{number}.jpg");

        Assert.Equal(names, shuffled.OrderBy(name => name, NaturalFilenameComparer.Instance));
    }

    [Fact]
    public void NullComesBeforeAnyName()
    {
        // Folder enumeration never produces one, but the order has to be defined for every input.
        var comparer = NaturalFilenameComparer.Instance;

        Assert.True(comparer.Compare(null, "0.jpg") < 0);
        Assert.True(comparer.Compare("0.jpg", null) > 0);
        Assert.Equal(0, comparer.Compare(null, null));
    }

    /// <summary>Checks that <paramref name="first"/> comes before <paramref name="second"/> in both directions.</summary>
    static void AssertComesFirst(string first, string second)
    {
        var comparer = NaturalFilenameComparer.Instance;

        Assert.True(comparer.Compare(first, second) < 0, $"Expected {first} before {second}.");
        Assert.True(comparer.Compare(second, first) > 0, $"Expected {second} after {first}.");
        Assert.Equal(0, comparer.Compare(first, first));
    }
}
