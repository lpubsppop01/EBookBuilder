using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>The contents of the build dialog.</summary>
public sealed class BuildSettings : ObservableObject
{
    string m_OutputFilePath = "";
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
                return "Packages the images as they are without re-encoding. The image quality does not change.";

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
        ImageFormatKind = ImageFormatKind,
        SizeKind = SizeKind,
        TargetSize = new ImageSize(Width, Height),
        DrawsCornerDots = DrawsCornerDots,
        JpegQuality = JpegQuality,
    };

    /// <summary>Notifies all at once for the properties determined by the combination of settings.</summary>
    void OnOutputSettingsChanged()
    {
        OnPropertyChanged(nameof(IsSizeSpecified));
        OnPropertyChanged(nameof(IsJpegQualitySpecified));
        OnPropertyChanged(nameof(OutputFormatDescription));
    }
}
