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
    /// The order is determined by "ordinal comparison of the name without the extension". This
    /// fixes a bug that was in the original WPF version. The original used the order returned by
    /// <c>Directory.EnumerateFiles</c> as-is and implicitly relied on Windows NTFS returning
    /// files in name order. On file systems such as ext4 the return order is nondeterministic,
    /// so as-is the pages would end up swapped.
    /// </para>
    /// <para>
    /// <see cref="StringComparer.Ordinal"/> is used for the comparison so that the order does
    /// not change with the locale (e.g. <c>10.jpg</c> coming before <c>9.jpg</c>).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> EnumeratePageFilenames(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return [];

        return new DirectoryInfo(directoryPath).GetFiles()
            .Where(file => PageFilePattern.IsMatch(file.Name))
            .OrderBy(file => Path.GetFileNameWithoutExtension(file.Name), StringComparer.Ordinal)
            .Select(file => file.Name)
            .ToArray();
    }
}
