using ExifLibrary;
using Lpubsppop01.EBookBuilder.Core.Exif;

namespace Lpubsppop01.EBookBuilder.Core.Tests.Exif;

/// <summary>
/// Verification of the rotation algebra. Exhaustively confirms that the four states form a cyclic group.
/// This is pure logic that depends on neither the UI nor file I/O,
/// so it is the key to guaranteeing that the meaning did not change during the port.
/// </summary>
public class ExifOrientationTests
{
    public static TheoryData<ExifOrientation> AllOrientations => new()
    {
        ExifOrientation.HorizontalNormal,
        ExifOrientation.Rotate90CW,
        ExifOrientation.Rotate180,
        ExifOrientation.Rotate270CW,
    };

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Rotating90DegreesFourTimesReturnsToOriginal(ExifOrientation orientation)
    {
        var actual = orientation.Rotated90().Rotated90().Rotated90().Rotated90();
        Assert.Equal(orientation, actual);
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Rotating180DegreesTwiceReturnsToOriginal(ExifOrientation orientation)
    {
        Assert.Equal(orientation, orientation.Rotated180().Rotated180());
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Rotating90And270DegreesCancelEachOther(ExifOrientation orientation)
    {
        Assert.Equal(orientation, orientation.Rotated90().Rotated270());
        Assert.Equal(orientation, orientation.Rotated270().Rotated90());
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Rotating180DegreesEqualsTwo90DegreeRotations(ExifOrientation orientation)
    {
        Assert.Equal(orientation.Rotated180(), orientation.Rotated90().Rotated90());
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Rotating270DegreesEqualsThree90DegreeRotations(ExifOrientation orientation)
    {
        Assert.Equal(orientation.Rotated270(), orientation.Rotated90().Rotated90().Rotated90());
    }

    [Theory]
    [InlineData(ExifOrientation.HorizontalNormal, RotationAmount.Deg90, ExifOrientation.Rotate90CW)]
    [InlineData(ExifOrientation.HorizontalNormal, RotationAmount.Deg180, ExifOrientation.Rotate180)]
    [InlineData(ExifOrientation.HorizontalNormal, RotationAmount.Deg270, ExifOrientation.Rotate270CW)]
    [InlineData(ExifOrientation.Rotate90CW, RotationAmount.Deg90, ExifOrientation.Rotate180)]
    [InlineData(ExifOrientation.Rotate90CW, RotationAmount.Deg180, ExifOrientation.Rotate270CW)]
    [InlineData(ExifOrientation.Rotate90CW, RotationAmount.Deg270, ExifOrientation.HorizontalNormal)]
    [InlineData(ExifOrientation.Rotate180, RotationAmount.Deg90, ExifOrientation.Rotate270CW)]
    [InlineData(ExifOrientation.Rotate270CW, RotationAmount.Deg270, ExifOrientation.Rotate180)]
    public void RotationAmountOverloadMatchesIndividualMethods(
        ExifOrientation start, RotationAmount amount, ExifOrientation expected)
    {
        Assert.Equal(expected, start.Rotated(amount));
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void NoRotationIsTheIdentity(ExifOrientation orientation)
    {
        Assert.Equal(orientation, orientation.Rotated(RotationAmount.None));
    }

    [Theory]
    [InlineData(ExifOrientation.HorizontalNormal, false)]
    [InlineData(ExifOrientation.Rotate90CW, true)]
    [InlineData(ExifOrientation.Rotate180, false)]
    [InlineData(ExifOrientation.Rotate270CW, true)]
    public void Only90And270DegreesSwapWidthAndHeight(ExifOrientation orientation, bool expected)
    {
        Assert.Equal(expected, orientation.SwapsWidthAndHeight());
    }

    [Theory]
    [InlineData(Orientation.Normal, ExifOrientation.HorizontalNormal)]
    [InlineData(Orientation.Rotated180, ExifOrientation.Rotate180)]
    [InlineData(Orientation.RotatedLeft, ExifOrientation.Rotate90CW)]
    [InlineData(Orientation.RotatedRight, ExifOrientation.Rotate270CW)]
    public void ExifValueIsConvertedToTheFourStates(Orientation value, ExifOrientation expected)
    {
        Assert.Equal(expected, value.ToExifOrientation());
    }

    [Theory]
    [InlineData(2)]  // horizontal mirror
    [InlineData(4)]  // vertical mirror
    [InlineData(5)]  // horizontal mirror + 270 degree rotation
    [InlineData(7)]  // horizontal mirror + 90 degree rotation
    [InlineData(0)]  // undefined
    [InlineData(9)]  // undefined
    public void MirroredAndUndefinedValuesFallBackToHorizontalNormal(int exifValue)
    {
        // This follows the behavior of the original app. The mirror information is lost,
        // but mirrored Orientation values hardly ever appear in scanned images.
        // Values are specified by EXIF specification numbers rather than member names.
        Assert.Equal(ExifOrientation.HorizontalNormal, ((Orientation)exifValue).ToExifOrientation());
    }

    [Fact]
    public void EnumValuesMatchExifSpecificationNumbers()
    {
        Assert.Equal(1, (byte)ExifOrientation.HorizontalNormal);
        Assert.Equal(3, (byte)ExifOrientation.Rotate180);
        Assert.Equal(6, (byte)ExifOrientation.Rotate90CW);
        Assert.Equal(8, (byte)ExifOrientation.Rotate270CW);
    }
}
