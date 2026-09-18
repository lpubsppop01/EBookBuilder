using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.Core.Build;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>Verification of enabling and disabling the build settings input fields.</summary>
public class BuildSettingsTests
{
    static BuildSettings OriginalJpeg() => new()
    {
        SizeKind = BuildSizeKind.Original,
        ImageFormatKind = BuildImageFormatKind.Jpeg,
        DrawsCornerDots = false,
    };

    #region Size specification

    [Fact]
    public void WidthAndHeightAreDisabledAtOriginalSize()
    {
        var settings = OriginalJpeg();
        Assert.False(settings.IsSizeSpecified);
    }

    [Fact]
    public void WidthAndHeightAreEnabledForSpecifiedSize()
    {
        var settings = OriginalJpeg();
        settings.SizeKind = BuildSizeKind.Specified;
        Assert.True(settings.IsSizeSpecified);
    }

    #endregion

    #region Quality

    [Fact]
    public void QualityIsDisabledWhenNothingIsReEncoded()
    {
        // Original size, no corner dots and JPEG means a straight copy, so changing the quality makes no difference.
        Assert.False(OriginalJpeg().IsJpegQualitySpecified);
    }

    [Fact]
    public void QualityIsEnabledForSpecifiedSize()
    {
        var settings = OriginalJpeg();
        settings.SizeKind = BuildSizeKind.Specified;
        Assert.True(settings.IsJpegQualitySpecified);
    }

    [Fact]
    public void QualityIsEnabledWhenCornerDotsAreDrawn()
    {
        var settings = OriginalJpeg();
        settings.DrawsCornerDots = true;
        Assert.True(settings.IsJpegQualitySpecified);
    }

    [Fact]
    public void QualityIsDisabledForLosslessPng()
    {
        var settings = OriginalJpeg();
        settings.ImageFormatKind = BuildImageFormatKind.Png;
        Assert.False(settings.IsJpegQualitySpecified);
    }

    [Fact]
    public void QualityIsDisabledForPngEvenWithSpecifiedSize()
    {
        var settings = OriginalJpeg();
        settings.ImageFormatKind = BuildImageFormatKind.Png;
        settings.SizeKind = BuildSizeKind.Specified;
        Assert.False(settings.IsJpegQualitySpecified);
    }

    [Fact]
    public void ChangingASettingRaisesNotificationsAboutEnablement()
    {
        var settings = OriginalJpeg();
        var notified = new List<string?>();
        settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        settings.SizeKind = BuildSizeKind.Specified;

        Assert.Contains(nameof(BuildSettings.IsJpegQualitySpecified), notified);
        Assert.Contains(nameof(BuildSettings.IsSizeSpecified), notified);
        Assert.Contains(nameof(BuildSettings.OutputFormatDescription), notified);
    }

    #endregion

    #region Description text

    [Fact]
    public void DescriptionSaysQualityIsUnchangedWhenNothingIsReEncoded()
    {
        Assert.Contains("image quality does not change", OriginalJpeg().OutputFormatDescription);
    }

    [Fact]
    public void DescriptionTellsTheQualityWhenDownscaling()
    {
        var settings = OriginalJpeg();
        settings.SizeKind = BuildSizeKind.Specified;
        settings.JpegQuality = 88;
        Assert.Contains("88", settings.OutputFormatDescription);
    }

    [Fact]
    public void DescriptionSaysPngConversionReEncodes()
    {
        var settings = OriginalJpeg();
        settings.ImageFormatKind = BuildImageFormatKind.Png;
        // Assert on the part that distinguishes this case, not just "re-encodes"
        Assert.Contains("PNG", settings.OutputFormatDescription);
    }

    [Fact]
    public void DescriptionSaysDrawingCornerDotsReEncodes()
    {
        var settings = OriginalJpeg();
        settings.DrawsCornerDots = true;
        Assert.Contains("corner dots", settings.OutputFormatDescription);
    }

    #endregion

    #region Conversion to the core settings

    [Fact]
    public void ValuesAreCarriedOverToTheCoreOptions()
    {
        var settings = new BuildSettings
        {
            OutputFilePath = "/tmp/book.cbz",
            ImageFormatKind = BuildImageFormatKind.Png,
            SizeKind = BuildSizeKind.Specified,
            Width = 800,
            Height = 1200,
            DrawsCornerDots = true,
            JpegQuality = 77,
        };

        var options = settings.ToBuildOptions();

        Assert.Equal("/tmp/book.cbz", options.OutputFilePath);
        Assert.Equal(BuildImageFormatKind.Png, options.ImageFormatKind);
        Assert.Equal(BuildSizeKind.Specified, options.SizeKind);
        Assert.Equal(800, options.TargetSize.Width);
        Assert.Equal(1200, options.TargetSize.Height);
        Assert.True(options.DrawsCornerDots);
        Assert.Equal(77, options.JpegQuality);
    }

    [Fact]
    public void QualityDefaultsToNinety()
    {
        Assert.Equal(90, new BuildSettings().JpegQuality);
    }

    #endregion

    #region Container

    [Fact]
    public void TheContainerDefaultsToCbz()
    {
        Assert.Equal(BuildContainerKind.Cbz, new BuildSettings().ContainerKind);
    }

    [Fact]
    public void TheImageFormatIsOnlyOfferedForACbz()
    {
        var settings = OriginalJpeg();
        Assert.True(settings.IsImageFormatSpecified);

        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.False(settings.IsImageFormatSpecified);
    }

    [Fact]
    public void ThePageResolutionIsOnlyOfferedForAPdf()
    {
        var settings = OriginalJpeg();
        Assert.False(settings.IsPdfPageDpiSpecified);

        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.True(settings.IsPdfPageDpiSpecified);
    }

    [Fact]
    public void ChangingTheContainerRaisesNotificationsAboutEnablement()
    {
        var settings = OriginalJpeg();
        var notified = new List<string?>();
        settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        settings.ContainerKind = BuildContainerKind.Pdf;

        Assert.Contains(nameof(BuildSettings.IsImageFormatSpecified), notified);
        Assert.Contains(nameof(BuildSettings.IsPdfPageDpiSpecified), notified);
        Assert.Contains(nameof(BuildSettings.OutputFormatDescription), notified);
    }

    /// <summary>
    /// A PDF stores JPEG page images, so the format is forced. The setting itself is kept, so that
    /// switching back to a CBZ brings the user's choice back rather than silently losing it.
    /// </summary>
    [Fact]
    public void APdfForcesJpegWithoutLosingTheChosenFormat()
    {
        var settings = OriginalJpeg();
        settings.ImageFormatKind = BuildImageFormatKind.Png;
        settings.ContainerKind = BuildContainerKind.Pdf;

        Assert.Equal(BuildImageFormatKind.Jpeg, settings.ToBuildOptions().ImageFormatKind);
        Assert.Equal(BuildImageFormatKind.Png, settings.ImageFormatKind);

        settings.ContainerKind = BuildContainerKind.Cbz;
        Assert.Equal(BuildImageFormatKind.Png, settings.ToBuildOptions().ImageFormatKind);
    }

    /// <summary>A PDF never gets a sentence about converting to PNG, whatever else is set.</summary>
    [Theory]
    [InlineData(BuildSizeKind.Original, false)]
    [InlineData(BuildSizeKind.Original, true)]
    [InlineData(BuildSizeKind.Specified, false)]
    [InlineData(BuildSizeKind.Specified, true)]
    public void APdfIsNeverDescribedAsConvertingToPng(BuildSizeKind sizeKind, bool drawsCornerDots)
    {
        var settings = OriginalJpeg();
        settings.ImageFormatKind = BuildImageFormatKind.Png;
        settings.ContainerKind = BuildContainerKind.Pdf;
        settings.SizeKind = sizeKind;
        settings.DrawsCornerDots = drawsCornerDots;

        Assert.DoesNotContain("PNG", settings.OutputFormatDescription);
    }

    [Fact]
    public void DescriptionSaysEmbeddedRatherThanPackagedForAPdf()
    {
        var settings = OriginalJpeg();
        settings.ContainerKind = BuildContainerKind.Pdf;

        Assert.Contains("Embeds the images as they are", settings.OutputFormatDescription);
        Assert.Contains("image quality does not change", settings.OutputFormatDescription);
    }

    [Fact]
    public void ThePageResolutionIsCarriedOverToTheCoreOptions()
    {
        var settings = new BuildSettings
        {
            OutputFilePath = "/tmp/book.pdf",
            ContainerKind = BuildContainerKind.Pdf,
            PdfPageDpi = 150,
        };

        Assert.Equal(150, settings.ToBuildOptions().PdfPageDpi);
        Assert.Equal(300, new BuildSettings().PdfPageDpi);
    }

    #endregion

    #region Output name

    [Fact]
    public void TheDefaultOutputNameFollowsTheContainer()
    {
        Assert.Equal("/tmp/scans.cbz", BuildSettings.DefaultOutputFilePath("/tmp/scans", BuildContainerKind.Cbz));
        Assert.Equal("/tmp/scans.pdf", BuildSettings.DefaultOutputFilePath("/tmp/scans", BuildContainerKind.Pdf));
    }

    [Fact]
    public void ChangingTheContainerRenamesAnOutputWeChose()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/scans.cbz" };
        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.Equal("/tmp/scans.pdf", settings.OutputFilePath);

        settings.ContainerKind = BuildContainerKind.Cbz;
        Assert.Equal("/tmp/scans.cbz", settings.OutputFilePath);
    }

    [Fact]
    public void ChangingTheContainerAlsoRenamesAZipName()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/scans.zip" };
        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.Equal("/tmp/scans.pdf", settings.OutputFilePath);
    }

    /// <summary>
    /// A name the user typed is not ours to change. Renaming it would be worse than leaving it,
    /// since nothing in the name says it is ours.
    /// </summary>
    [Fact]
    public void ChangingTheContainerLeavesAChosenNameAlone()
    {
        var settings = new BuildSettings { OutputFilePath = "/tmp/book.2024-09" };
        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.Equal("/tmp/book.2024-09", settings.OutputFilePath);
    }

    [Fact]
    public void ChangingTheContainerLeavesAnEmptyNameAlone()
    {
        var settings = new BuildSettings { OutputFilePath = "" };
        settings.ContainerKind = BuildContainerKind.Pdf;
        Assert.Equal("", settings.OutputFilePath);
    }

    #endregion
}
