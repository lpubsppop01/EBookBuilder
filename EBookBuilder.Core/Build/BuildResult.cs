namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>Result of CBZ export.</summary>
/// <param name="OutputFilePath">Path of the created CBZ.</param>
/// <param name="PageCount">Number of pages included.</param>
/// <param name="CopiedCount">Number of pages copied without re-encoding.</param>
public readonly record struct BuildResult(
    string OutputFilePath,
    int PageCount,
    int CopiedCount)
{
    /// <summary>Number of pages re-encoded.</summary>
    public int ReEncodedCount => PageCount - CopiedCount;
}
