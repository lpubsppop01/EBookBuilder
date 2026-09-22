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
    /// <remarks>
    /// <para>
    /// The value is passed as a <see cref="ushort"/> because Orientation is defined as an EXIF
    /// SHORT. The library also has a byte overload, and passing a byte writes the tag with the
    /// EXIF type BYTE instead: the library's own reader then reads the value in the file's byte
    /// order, so on a big-endian (camera-style) file it came back as a value that is not a valid
    /// orientation and every rotation looked like it had done nothing.
    /// </para>
    /// <para>
    /// A BYTE-typed Orientation is also invalid as far as other software is concerned and gets
    /// ignored, so the rotation was invisible there as well, whatever the byte order.
    /// </para>
    /// </remarks>
    public static void Write(string path, ExifOrientation orientation)
    {
        var file = ImageFile.FromFile(path);
        file.Properties.Set(ExifTag.Orientation, (ushort)orientation);
        file.Save(path);
    }
}
