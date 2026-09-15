using ExifLibrary;

namespace Lpubsppop01.EBookBuilder.Core.Exif;

/// <summary>
/// EXIF Orientation used to correct the rotation of page images. Handles only the 4 states
/// that do not involve mirroring.
/// </summary>
/// <remarks>
/// This app corrects rotation by rewriting this tag rather than the pixel data. No re-encoding
/// occurs, so image quality does not degrade no matter how many times a page is rotated.
/// The numeric values are the Orientation values from the EXIF specification itself.
/// </remarks>
public enum ExifOrientation
{
    /// <summary>No rotation (EXIF value 1).</summary>
    HorizontalNormal = 1,

    /// <summary>180 degree rotation (EXIF value 3).</summary>
    Rotate180 = 3,

    /// <summary>90 degree clockwise rotation (EXIF value 6).</summary>
    Rotate90CW = 6,

    /// <summary>270 degree clockwise rotation (EXIF value 8).</summary>
    Rotate270CW = 8,
}

/// <summary>Amount of rotation the user requests in one operation.</summary>
public enum RotationAmount
{
    /// <summary>Do not rotate.</summary>
    None = 0,

    /// <summary>90 degrees clockwise.</summary>
    Deg90 = 90,

    /// <summary>180 degrees.</summary>
    Deg180 = 180,

    /// <summary>270 degrees clockwise (90 degrees counterclockwise).</summary>
    Deg270 = 270,
}

/// <summary>
/// Rotation algebra for <see cref="ExifOrientation"/>.
/// The 4 states form a cyclic group, so every rotation is a closed operation.
/// </summary>
public static class ExifOrientationExtensions
{
    /// <summary>
    /// Converts the EXIF library's <see cref="Orientation"/> to this app's 4 states.
    /// </summary>
    /// <remarks>
    /// Mirrored variants (2, 4, 5, 7) and undefined values fall back to
    /// <see cref="ExifOrientation.HorizontalNormal"/>.
    /// This follows the behavior of the original WPF version as-is, and the mirroring
    /// information is lost.
    /// </remarks>
    public static ExifOrientation ToExifOrientation(this Orientation orientation) => orientation switch
    {
        Orientation.Normal => ExifOrientation.HorizontalNormal,
        Orientation.Rotated180 => ExifOrientation.Rotate180,
        Orientation.RotatedLeft => ExifOrientation.Rotate90CW,
        Orientation.RotatedRight => ExifOrientation.Rotate270CW,
        _ => ExifOrientation.HorizontalNormal,
    };

    /// <summary>Returns the result of rotating 90 degrees clockwise.</summary>
    public static ExifOrientation Rotated90(this ExifOrientation orientation) => orientation switch
    {
        ExifOrientation.HorizontalNormal => ExifOrientation.Rotate90CW,
        ExifOrientation.Rotate180 => ExifOrientation.Rotate270CW,
        ExifOrientation.Rotate90CW => ExifOrientation.Rotate180,
        ExifOrientation.Rotate270CW => ExifOrientation.HorizontalNormal,
        _ => ExifOrientation.HorizontalNormal,
    };

    /// <summary>Returns the result of rotating 180 degrees.</summary>
    public static ExifOrientation Rotated180(this ExifOrientation orientation) => orientation switch
    {
        ExifOrientation.HorizontalNormal => ExifOrientation.Rotate180,
        ExifOrientation.Rotate180 => ExifOrientation.HorizontalNormal,
        ExifOrientation.Rotate90CW => ExifOrientation.Rotate270CW,
        ExifOrientation.Rotate270CW => ExifOrientation.Rotate90CW,
        _ => ExifOrientation.HorizontalNormal,
    };

    /// <summary>Returns the result of rotating 270 degrees clockwise.</summary>
    public static ExifOrientation Rotated270(this ExifOrientation orientation) => orientation switch
    {
        ExifOrientation.HorizontalNormal => ExifOrientation.Rotate270CW,
        ExifOrientation.Rotate180 => ExifOrientation.Rotate90CW,
        ExifOrientation.Rotate90CW => ExifOrientation.HorizontalNormal,
        ExifOrientation.Rotate270CW => ExifOrientation.Rotate180,
        _ => ExifOrientation.HorizontalNormal,
    };

    /// <summary>Returns the result of rotating by the specified amount.</summary>
    public static ExifOrientation Rotated(this ExifOrientation orientation, RotationAmount amount) => amount switch
    {
        RotationAmount.Deg90 => orientation.Rotated90(),
        RotationAmount.Deg180 => orientation.Rotated180(),
        RotationAmount.Deg270 => orientation.Rotated270(),
        _ => orientation,
    };

    /// <summary>Whether this orientation swaps width and height (90/270 degrees).</summary>
    public static bool SwapsWidthAndHeight(this ExifOrientation orientation) =>
        orientation is ExifOrientation.Rotate90CW or ExifOrientation.Rotate270CW;
}
