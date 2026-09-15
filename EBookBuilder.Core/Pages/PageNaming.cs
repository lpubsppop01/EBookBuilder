namespace Lpubsppop01.EBookBuilder.Core.Pages;

/// <summary>
/// Serial number rules for page filenames.
/// </summary>
/// <remarks>
/// Serial numbers are used to guarantee that "page order = ordinal order of the filenames".
/// The number of digits is derived from the count, so 10 pages give <c>00.jpg</c> to <c>09.jpg</c>.
/// </remarks>
public static class PageNaming
{
    /// <summary>Extension used by the serial number rename. Input is normalized to .jpg even if it is .jpeg.</summary>
    public const string SerialNumberExtension = ".jpg";

    /// <summary>Prefix added temporarily to avoid collisions.</summary>
    internal const string TempPrefix = "temp_";

    /// <summary>
    /// Builds the format string suited to the specified count. Example: 100 pages → <c>{0:000}.jpg</c>
    /// </summary>
    public static string BuildFilenameFormat(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The count is negative.");

        var digits = 0;
        for (var remaining = count; remaining > 0; remaining /= 10) ++digits;

        return "{0:" + new string('0', digits) + "}" + SerialNumberExtension;
    }

    /// <summary>Returns the filename given to the page at position <paramref name="index"/>.</summary>
    public static string FormatFilename(int index, int count) =>
        string.Format(BuildFilenameFormat(count), index);

    /// <summary>
    /// Whether the filenames form serial numbers matching the count. An empty list is true.
    /// </summary>
    public static bool AreSerialNumbers(IReadOnlyList<string> filenames)
    {
        for (var i = 0; i < filenames.Count; ++i)
        {
            if (filenames[i] != FormatFilename(i, filenames.Count)) return false;
        }
        return true;
    }
}
