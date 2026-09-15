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

/// <summary>Settings for CBZ export.</summary>
public sealed record BuildOptions
{
    /// <summary>Path of the CBZ file to output.</summary>
    public required string OutputFilePath { get; init; }

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
    /// Whether files can be packaged as-is without re-encoding.
    /// </summary>
    /// <remarks>
    /// Only when keeping the original size, drawing no corner dots and outputting JPEG can the
    /// bytes be copied verbatim. This is a deliberate difference from the original, which always
    /// re-encoded even when the original size was specified.
    /// </remarks>
    public bool CanCopyWithoutReEncoding =>
        SizeKind == BuildSizeKind.Original
        && !DrawsCornerDots
        && ImageFormatKind == BuildImageFormatKind.Jpeg;
}
