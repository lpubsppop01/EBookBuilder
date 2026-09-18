using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>The contents of the build dialog.</summary>
public sealed class BuildSettings : ObservableObject
{
    /// <summary>The extensions the output name is allowed to be renamed from.</summary>
    /// <remarks>A name ending in one of these is taken to be one we chose, not one the user typed.</remarks>
    static readonly string[] KnownContainerExtensions = [".cbz", ".zip", ".pdf"];

    string m_OutputFilePath = "";
    BuildContainerKind m_ContainerKind = BuildContainerKind.Cbz;
    int m_PdfPageDpi = BuildOptions.DefaultPdfPageDpi;
    BuildImageFormatKind m_ImageFormatKind = BuildImageFormatKind.Jpeg;
    BuildSizeKind m_SizeKind = BuildSizeKind.Original;
    int m_Width = 600;
    int m_Height = 1024;
    bool m_DrawsCornerDots;
    int m_JpegQuality = PageImagePipeline.DefaultJpegQuality;

    /// <summary>The path to write the output to.</summary>
    public string OutputFilePath
    {
        get => m_OutputFilePath;
        set => SetProperty(ref m_OutputFilePath, value);
    }

    /// <summary>The container to write the pages into.</summary>
    public BuildContainerKind ContainerKind
    {
        get => m_ContainerKind;
        set
        {
            if (!SetProperty(ref m_ContainerKind, value)) return;
            FollowContainerExtension(value);
            OnContainerChanged();
        }
    }

    /// <summary>The resolution the PDF pages are given.</summary>
    public int PdfPageDpi
    {
        get => m_PdfPageDpi;
        set => SetProperty(ref m_PdfPageDpi, value);
    }

    /// <summary>The image format to output.</summary>
    public BuildImageFormatKind ImageFormatKind
    {
        get => m_ImageFormatKind;
        set
        {
            if (SetProperty(ref m_ImageFormatKind, value)) OnOutputSettingsChanged();
        }
    }

    /// <summary>How the output size is specified.</summary>
    public BuildSizeKind SizeKind
    {
        get => m_SizeKind;
        set
        {
            if (SetProperty(ref m_SizeKind, value)) OnOutputSettingsChanged();
        }
    }

    /// <summary>Width of the specified size.</summary>
    public int Width
    {
        get => m_Width;
        set => SetProperty(ref m_Width, value);
    }

    /// <summary>Height of the specified size.</summary>
    public int Height
    {
        get => m_Height;
        set => SetProperty(ref m_Height, value);
    }

    /// <summary>Whether to draw the verification dots in the four corners.</summary>
    public bool DrawsCornerDots
    {
        get => m_DrawsCornerDots;
        set
        {
            if (SetProperty(ref m_DrawsCornerDots, value)) OnOutputSettingsChanged();
        }
    }

    /// <summary>JPEG quality when re-encoding.</summary>
    public int JpegQuality
    {
        get => m_JpegQuality;
        set => SetProperty(ref m_JpegQuality, value);
    }

    /// <summary>Whether the width and height inputs are enabled.</summary>
    public bool IsSizeSpecified => SizeKind == BuildSizeKind.Specified;

    /// <summary>The extension a container uses.</summary>
    public static string DefaultExtensionFor(BuildContainerKind containerKind) =>
        containerKind == BuildContainerKind.Pdf ? ".pdf" : ".cbz";

    /// <summary>The output path to suggest for a folder, before the user has chosen anything.</summary>
    public static string DefaultOutputFilePath(string targetDirectoryPath, BuildContainerKind containerKind) =>
        targetDirectoryPath + DefaultExtensionFor(containerKind);

    /// <summary>
    /// Whether the image format can be chosen.
    /// </summary>
    /// <remarks>
    /// PDF has no PNG image type, so the choice does not apply to it.
    /// </remarks>
    public bool IsImageFormatSpecified => ContainerKind == BuildContainerKind.Cbz;

    /// <summary>Whether the page resolution can be chosen. It only means something for a PDF.</summary>
    public bool IsPdfPageDpiSpecified => ContainerKind == BuildContainerKind.Pdf;

    /// <summary>
    /// Whether specifying the quality has any meaning.
    /// </summary>
    /// <remarks>
    /// When nothing is re-encoded, changing the quality does not change the result, so it is not offered.
    /// PNG is also lossless, so the concept of quality does not apply to it.
    /// The decision follows <see cref="BuildOptions.CanCopyWithoutReEncoding"/>.
    /// </remarks>
    public bool IsJpegQualitySpecified =>
        ImageFormatKind == BuildImageFormatKind.Jpeg && !ToBuildOptions().CanCopyWithoutReEncoding;

    /// <summary>A sentence telling the user whether the images will be re-encoded.</summary>
    /// <remarks>
    /// The original WPF version always re-encoded even when no size was specified,
    /// which made the drop in image quality easy to miss. Here the result is stated explicitly.
    /// </remarks>
    public string OutputFormatDescription
    {
        get
        {
            if (ToBuildOptions().CanCopyWithoutReEncoding)
                return ContainerKind == BuildContainerKind.Pdf
                    ? "Embeds the images as they are without re-encoding. The image quality does not change."
                    : "Packages the images as they are without re-encoding. The image quality does not change.";

            // Only PNG reaches this line, at the original size with no dots: JPEG would have taken
            // the copy path above. A PDF is always JPEG and is covered by that same path, so a PDF
            // never gets told it is being converted to PNG.
            if (SizeKind == BuildSizeKind.Original && !DrawsCornerDots)
                return "Re-encodes to convert to PNG.";

            if (DrawsCornerDots && SizeKind == BuildSizeKind.Original)
                return "Re-encodes to draw the corner dots.";

            if (DrawsCornerDots)
                return $"Resizes to the specified size, draws the corner dots, and re-encodes.";

            return $"Resizes to the specified size and re-encodes with quality {JpegQuality}.";
        }
    }

    /// <summary>Converts to the Core settings.</summary>
    public BuildOptions ToBuildOptions() => new()
    {
        OutputFilePath = OutputFilePath,
        ContainerKind = ContainerKind,
        // A PDF stores JPEG page images, so the format chosen for a CBZ does not apply to it.
        // The setting itself is left alone, so switching back to CBZ restores the user's choice.
        ImageFormatKind = ContainerKind == BuildContainerKind.Pdf ? BuildImageFormatKind.Jpeg : ImageFormatKind,
        SizeKind = SizeKind,
        TargetSize = new ImageSize(Width, Height),
        DrawsCornerDots = DrawsCornerDots,
        JpegQuality = JpegQuality,
        PdfPageDpi = PdfPageDpi,
    };

    /// <summary>
    /// Renames the output to match the container, when the name still looks like the one we chose.
    /// </summary>
    /// <remarks>
    /// A name the user typed is left alone. Without this, switching to PDF would leave a ZIP
    /// sitting under a <c>.pdf</c> name.
    /// </remarks>
    void FollowContainerExtension(BuildContainerKind containerKind)
    {
        if (string.IsNullOrEmpty(m_OutputFilePath)) return;

        var extension = Path.GetExtension(m_OutputFilePath);
        if (!KnownContainerExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return;

        OutputFilePath = Path.ChangeExtension(m_OutputFilePath, DefaultExtensionFor(containerKind));
    }

    /// <summary>Notifies all at once for the properties determined by the combination of settings.</summary>
    void OnOutputSettingsChanged()
    {
        OnPropertyChanged(nameof(IsSizeSpecified));
        OnPropertyChanged(nameof(IsJpegQualitySpecified));
        OnPropertyChanged(nameof(OutputFormatDescription));
    }

    /// <summary>Notifies for the properties determined by the container.</summary>
    /// <remarks>
    /// The description is raised again here even though <see cref="BuildOptions.CanCopyWithoutReEncoding"/>
    /// does not change with the container, because the sentence it produces does.
    /// </remarks>
    void OnContainerChanged()
    {
        OnOutputSettingsChanged();
        OnPropertyChanged(nameof(IsImageFormatSpecified));
        OnPropertyChanged(nameof(IsPdfPageDpiSpecified));
    }
}
