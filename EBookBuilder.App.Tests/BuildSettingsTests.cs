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
}
