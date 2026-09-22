using System.Text.RegularExpressions;

namespace Lpubsppop01.EBookBuilder.Core.Pages;

/// <summary>Enumeration of the folder holding the page images.</summary>
public static partial class PageFolder
{
    [GeneratedRegex(@".*\.(jpg|jpeg)$", RegexOptions.IgnoreCase)]
    private static partial Regex PageFilePattern { get; }

    /// <summary>Checks that the folder exists.</summary>
    public static bool Exists(string directoryPath) => Directory.Exists(directoryPath);

    /// <summary>
    /// Returns the page image filenames ordered in page order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the natural order of the filename: a run of digits is compared as a number, so
    /// <c>9.jpg</c> comes before <c>10.jpg</c>, and a folder whose numbers are not zero padded
    /// (<c>page - 1.jpg</c> … <c>page - 102.jpg</c>) is still read in page order. A name that is
    /// already a serial number is unaffected, because a single digit count makes the natural order
    /// and the ordinal order the same.
    /// </para>
    /// <para>
    /// This also fixes a bug that was in the original WPF version. The original used the order
    /// returned by <c>Directory.EnumerateFiles</c> as-is and implicitly relied on Windows NTFS
    /// returning files in name order. On file systems such as ext4 the return order is
    /// nondeterministic, so as-is the pages would end up swapped. The pages are now sorted
    /// explicitly, and <see cref="NaturalFilenameComparer"/> settles every pair, so the result is
    /// the same on every file system.
    /// </para>
    /// <para>
    /// The comparison is ordinal apart from the digit runs, so it does not change with the locale.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> EnumeratePageFilenames(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return [];

        return new DirectoryInfo(directoryPath).GetFiles()
            .Where(file => PageFilePattern.IsMatch(file.Name))
            .OrderBy(file => file.Name, NaturalFilenameComparer.Instance)
            .Select(file => file.Name)
            .ToArray();
    }
}
