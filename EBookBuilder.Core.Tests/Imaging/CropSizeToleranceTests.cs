using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Imaging;

/// <summary>
/// Verification of the tolerance used to decide whether pages can be cropped together.
/// </summary>
/// <remarks>
/// The sizes in the tests are those of an A4 page: 2480 x 3508 at 300dpi and 4961 x 7016 at 600dpi.
/// </remarks>
public class CropSizeToleranceTests
{
    #region The tolerance itself

    [Fact]
    public void ASmallImageAllowsTheMinimumNumberOfPixels()
    {
        // 1.5% of 100 pixels is 1.5 pixels, which is unusable, so the fixed minimum applies
        Assert.Equal(CropSizeTolerance.MinimumPixels, CropSizeTolerance.AllowedDifference(100));
    }

    [Fact]
    public void TheToleranceGrowsWithTheLength()
    {
        // 1.5% of 2480 is 37.2 pixels
        Assert.Equal(38, CropSizeTolerance.AllowedDifference(2480));
        Assert.Equal(53, CropSizeTolerance.AllowedDifference(3508));
    }

    [Fact]
    public void TheToleranceAt600DpiCoversTheSamePhysicalDifference()
    {
        // Doubling the resolution doubles the number of pixels the same skew moves the edge by
        Assert.Equal(75, CropSizeTolerance.AllowedDifference(4961));
        Assert.Equal(106, CropSizeTolerance.AllowedDifference(7016));
    }

    #endregion

    #region Comparison

    [Fact]
    public void IdenticalSizesAreTheSameSize()
    {
        Assert.True(CropSizeTolerance.AreSameSize(new ImageSize(2480, 3508), new ImageSize(2480, 3508)));
    }

    [Fact]
    public void DifferencesThatRealScansProducedAreAccepted()
    {
        // The pages of one book came out this far apart (13 pixels, 0.89% of the width)
        Assert.True(CropSizeTolerance.AreSameSize(new ImageSize(1459, 2048), new ImageSize(1446, 2048)));
        Assert.True(CropSizeTolerance.AreSameSize(new ImageSize(1446, 2048), new ImageSize(1459, 2048)));
    }

    [Theory]
    [InlineData(2518, 3508)]   // the width is at the limit (38 pixels)
    [InlineData(2442, 3508)]
    [InlineData(2480, 3561)]   // the height is at the limit (53 pixels)
    [InlineData(2493, 3526)]   // both dimensions differ, which is what a scan produces
    public void DifferencesWithinTheToleranceAreAccepted(int width, int height)
    {
        Assert.True(CropSizeTolerance.AreSameSize(new ImageSize(2480, 3508), new ImageSize(width, height)));
    }

    [Theory]
    [InlineData(2519, 3508)]   // one pixel over the limit
    [InlineData(2480, 3562)]
    [InlineData(2400, 3508)]   // beyond the tolerance, but still nowhere near a different format
    [InlineData(3508, 2480)]   // landscape, i.e. a different format altogether
    public void DifferencesBeyondTheToleranceAreRejected(int width, int height)
    {
        Assert.False(CropSizeTolerance.AreSameSize(new ImageSize(2480, 3508), new ImageSize(width, height)));
    }

    [Fact]
    public void AHighResolutionScanAcceptsADifferenceALowResolutionOneRejects()
    {
        // The same 50 pixel difference: accepted at 600dpi (the tolerance is 75 pixels),
        // rejected at 300dpi (38 pixels). A fixed number of pixels could not do both.
        Assert.True(CropSizeTolerance.AreSameSize(new ImageSize(4961, 7016), new ImageSize(5011, 7016)));
        Assert.False(CropSizeTolerance.AreSameSize(new ImageSize(2480, 3508), new ImageSize(2530, 3508)));
    }

    [Fact]
    public void TheDifferenceIsTheLargerOfTheTwoDimensions()
    {
        Assert.Equal(12, CropSizeTolerance.Difference(new ImageSize(2480, 3508), new ImageSize(2483, 3520)));
        Assert.Equal(0, CropSizeTolerance.Difference(new ImageSize(2480, 3508), new ImageSize(2480, 3508)));
    }

    #endregion
}
