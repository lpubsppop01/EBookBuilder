namespace Lpubsppop01.EBookBuilder.Core.Pages;

/// <summary>
/// Compares filenames the way a person reads them: a run of digits is compared as a number.
/// </summary>
/// <remarks>
/// <para>
/// A plain ordinal comparison puts <c>10.jpg</c> before <c>9.jpg</c>, so a folder of scans whose
/// numbers are not zero padded (<c>page - 1.jpg</c> … <c>page - 102.jpg</c>) comes out in an order
/// that has nothing to do with the page order, and building from it produces a book in that order.
/// Here the names are walked from the start, and a position where both names have a digit is
/// compared as a number instead.
/// </para>
/// <para>
/// The digits are never parsed into a number. A run is compared by its length once the leading
/// zeros are dropped, and then digit by digit, so a run of any length compares correctly and
/// nothing can overflow.
/// </para>
/// <para>
/// Everything else stays an ordinal comparison, so the locale does not enter into it, and a digit
/// meeting a letter at the same position still puts the digit first (<c>1.jpg</c> before
/// <c>c.jpg</c>). Names that are already serial numbers (<c>001.jpg</c>, <c>002.jpg</c>) therefore
/// keep the order they had under the ordinal comparison.
/// </para>
/// <para>
/// Two different names never compare as equal, not even <c>1.jpg</c> and <c>01.jpg</c> (the one
/// written with fewer leading zeros comes first). The order is therefore total, and a page list
/// sorted with this does not depend on the order the file system happened to return the files in.
/// </para>
/// </remarks>
public sealed class NaturalFilenameComparer : IComparer<string>
{
    /// <summary>The comparer to use. It holds no state, so a single instance is enough.</summary>
    public static NaturalFilenameComparer Instance { get; } = new();

    NaturalFilenameComparer()
    {
    }

    /// <summary>Compares two filenames. A null name sorts before any name.</summary>
    /// <returns>A negative value when <paramref name="x"/> comes first, and 0 only when they are the same name.</returns>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var i = 0;
        var j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var result = CompareDigitRuns(x, ref i, y, ref j);
                if (result != 0) return result;
            }
            else
            {
                var result = x[i].CompareTo(y[j]);
                if (result != 0) return result;
                ++i;
                ++j;
            }
        }

        // One name is the start of the other, so the shorter one comes first.
        return (x.Length - i).CompareTo(y.Length - j);
    }

    /// <summary>Compares the digit runs that both positions stand on, and moves both past them.</summary>
    /// <remarks>
    /// Runs of the same value written with a different number of leading zeros (<c>1</c> and
    /// <c>01</c>) are not equal as names, so the one that has fewer of them is put first.
    /// </remarks>
    static int CompareDigitRuns(string x, ref int i, string y, ref int j)
    {
        var xStart = i;
        var yStart = j;
        while (i < x.Length && char.IsAsciiDigit(x[i])) ++i;
        while (j < y.Length && char.IsAsciiDigit(y[j])) ++j;

        var xRun = x.AsSpan(xStart, i - xStart);
        var yRun = y.AsSpan(yStart, j - yStart);

        // The leading zeros are not part of the value.
        var xDigits = xRun.TrimStart('0');
        var yDigits = yRun.TrimStart('0');

        // With the leading zeros gone, the longer run is the larger number.
        if (xDigits.Length != yDigits.Length) return xDigits.Length.CompareTo(yDigits.Length);

        var result = xDigits.SequenceCompareTo(yDigits);
        if (result != 0) return result;

        // The same value, so the run written with fewer leading zeros comes first.
        return (xRun.Length - xDigits.Length).CompareTo(yRun.Length - yDigits.Length);
    }
}
