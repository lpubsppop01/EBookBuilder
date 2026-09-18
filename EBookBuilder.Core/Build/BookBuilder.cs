namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>Writes the pages out into the container chosen by the options.</summary>
public static class BookBuilder
{
    /// <summary>
    /// Writes out the pages and creates the output file. An existing output file is overwritten.
    /// </summary>
    /// <exception cref="ArgumentException">When the settings cannot be built.</exception>
    /// <exception cref="InvalidOperationException">When the filenames are not serial numbers.</exception>
    public static Task<BuildResult> BuildAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        BuildOptions options,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        return options.ContainerKind switch
        {
            BuildContainerKind.Pdf =>
                PdfBuilder.BuildAsync(directoryPath, filenames, options, progress, cancellationToken),
            _ =>
                CbzBuilder.BuildAsync(directoryPath, filenames, options, progress, cancellationToken),
        };
    }
}
