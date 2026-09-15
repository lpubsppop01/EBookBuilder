using ExifLibrary;

namespace Lpubsppop01.EBookBuilder.Core.Exif;

/// <summary>
/// Reads and writes the EXIF Orientation tag of JPEG files.
/// </summary>
/// <remarks>
/// Writing replaces only the APP1 (EXIF) segment, so the image data is never re-encoded.
/// When the file size changes, the file is rewritten in place.
/// </remarks>
public static class ExifOrientationStore
{
    /// <summary>
    /// Reads the Orientation of the specified file. Returns
    /// <see cref="ExifOrientation.HorizontalNormal"/> when the tag is missing or cannot be
    /// interpreted.
    /// </summary>
    public static ExifOrientation Read(string path)
    {
        var file = ImageFile.FromFile(path);
        var property = file.Properties.Get<ExifEnumProperty<Orientation>>(ExifTag.Orientation);
        return property?.Value.ToExifOrientation() ?? ExifOrientation.HorizontalNormal;
    }

    /// <summary>
    /// Rewrites the Orientation of the specified file. The pixel data is not touched.
    /// </summary>
    public static void Write(string path, ExifOrientation orientation)
    {
        var file = ImageFile.FromFile(path);
        file.Properties.Set(ExifTag.Orientation, (byte)orientation);
        file.Save(path);
    }
}
