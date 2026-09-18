using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>Image format to output.</summary>
public enum BuildImageFormatKind
{
    /// <summary>JPEG. If the source is JPEG, it can be copied losslessly when no size is specified.</summary>
    Jpeg,

    /// <summary>PNG. Always re-encoded.</summary>
    Png,
}

/// <summary>Size to output.</summary>
public enum BuildSizeKind
{
    /// <summary>Keep the original size.</summary>
    Original,

    /// <summary>Shrink to fit within the specified frame.</summary>
    Specified,
}

/// <summary>Container to write the pages into.</summary>
public enum BuildContainerKind
{
    /// <summary>CBZ, a ZIP holding the page images.</summary>
    Cbz,

    /// <summary>PDF, with each page stored as one image XObject.</summary>
    Pdf,
}

/// <summary>Settings for the build.</summary>
public sealed record BuildOptions
{
    /// <summary>Default resolution for PDF page coordinates.</summary>
    public const int DefaultPdfPageDpi = 300;

    /// <summary>Path of the file to output.</summary>
    public required string OutputFilePath { get; init; }

    /// <summary>Container to write the pages into.</summary>
    public BuildContainerKind ContainerKind { get; init; } = BuildContainerKind.Cbz;

    /// <summary>Image format to output.</summary>
    public BuildImageFormatKind ImageFormatKind { get; init; } = BuildImageFormatKind.Jpeg;

    /// <summary>How the output size is specified.</summary>
    public BuildSizeKind SizeKind { get; init; } = BuildSizeKind.Original;

    /// <summary>
    /// The frame to fit within when <see cref="BuildSizeKind.Specified"/> is used.
    /// The aspect ratio is preserved, and the frame is never filled with margins.
    /// </summary>
    public ImageSize TargetSize { get; init; } = new(600, 1024);

    /// <summary>Whether to draw confirmation black dots at the four corners.</summary>
    public bool DrawsCornerDots { get; init; }

    /// <summary>Quality used when re-encoding JPEG.</summary>
    /// <remarks>
    /// The original WPF version fixed this at 75 and did not expose it in the UI. Here the default
    /// is raised, and the caller can lower it if needed.
    /// </remarks>
    public int JpegQuality { get; init; } = PageImagePipeline.DefaultJpegQuality;

    /// <summary>
    /// Resolution used to convert image pixels into PDF page coordinates.
    /// </summary>
    /// <remarks>
    /// A PDF page has a physical size, which the image pixels alone do not determine, so one has
    /// to be chosen. This is the same idea as Skia's <c>RasterDpi</c>. It only affects the size
    /// the pages are declared to have; the reader's fit-to-window is unaffected by it.
    /// </remarks>
    public int PdfPageDpi { get; init; } = DefaultPdfPageDpi;

    /// <summary>
    /// Whether files can be packaged as-is without re-encoding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is decided only by the size, the corner dots and the image format. The container does
    /// not enter into it: when this holds, a CBZ gets the bytes copied into the archive and a PDF
    /// gets them embedded as a DCTDecode image XObject, both without re-encoding. Since PDF always
    /// stores JPEG page images, for a PDF the condition reduces to keeping the original size and
    /// drawing no corner dots.
    /// </para>
    /// <para>
    /// This is a deliberate difference from the original, which always re-encoded even when the
    /// original size was specified.
    /// </para>
    /// </remarks>
    public bool CanCopyWithoutReEncoding =>
        SizeKind == BuildSizeKind.Original
        && !DrawsCornerDots
        && ImageFormatKind == BuildImageFormatKind.Jpeg;

    /// <summary>Checks that the settings can be built.</summary>
    /// <exception cref="ArgumentException">When a setting is blank or an impossible combination.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When a number is out of range.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OutputFilePath))
            throw new ArgumentException("The output path is empty.", nameof(OutputFilePath));

        // System.Text.Json turns an out-of-range number into an undefined enum value without
        // complaining, and the dispatch would then quietly fall through to CBZ.
        if (!Enum.IsDefined(ContainerKind))
            throw new ArgumentException($"Unknown container: {ContainerKind}", nameof(ContainerKind));

        if (ContainerKind == BuildContainerKind.Pdf && ImageFormatKind != BuildImageFormatKind.Jpeg)
            throw new ArgumentException("PDF always stores JPEG page images.", nameof(ImageFormatKind));

        if (PdfPageDpi is < 18 or > 2400)
            throw new ArgumentOutOfRangeException(nameof(PdfPageDpi), PdfPageDpi, "The page resolution must be between 18 and 2400.");
    }
}
