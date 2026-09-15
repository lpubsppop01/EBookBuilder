namespace Lpubsppop01.EBookBuilder.Core.Imaging;

/// <summary>Image dimensions in pixels.</summary>
public readonly record struct ImageSize(int Width, int Height)
{
    /// <summary>Returns the dimensions with width and height swapped (used for 90/270 degree rotation).</summary>
    public ImageSize Swapped => new(Height, Width);
}
