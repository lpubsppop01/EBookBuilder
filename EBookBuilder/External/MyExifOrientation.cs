using ExifLibrary;
using System.Windows.Media.Imaging;

namespace lpubsppop01.EBookBuilder.External
{
    enum MyExifOrientation
    {
        HorizontalNormal = 1,
        // MirrorHorizontal = 2,
        Rotate180 = 3,
        // MirrorVertical = 4,
        // MirrorHorizontalAndRotate270CW = 5,
        Rotate90CW = 6,
        // MirrorHorizontalAndRotate90CW = 7,
        Rotate270CW = 8
    }

    static class MyExifOrientationUtility
    {
        #region Read/Write

        public static MyExifOrientation Read(string path)
        {
            var file = ImageFile.FromFile(path);
            var property = file.Properties.Get<ExifEnumProperty<Orientation>>(ExifTag.Orientation);
            return property?.Value.ToMyExifOrientation() ?? MyExifOrientation.HorizontalNormal;
        }

        public static void Write(string path, MyExifOrientation orientation)
        {
            var file = ImageFile.FromFile(path);
            file.Properties.Set(ExifTag.Orientation, (byte)orientation);
            file.Save(path);
        }

        #endregion

        #region Conversion from/to ExifLibrary.Orientation

        public static MyExifOrientation ToMyExifOrientation(this Orientation orientation)
        {
            switch (orientation)
            {
                case Orientation.Normal:
                    return MyExifOrientation.HorizontalNormal;
                case Orientation.Rotated180:
                    return MyExifOrientation.Rotate180;
                case Orientation.RotatedLeft:
                    return MyExifOrientation.Rotate90CW;
                case Orientation.RotatedRight:
                    return MyExifOrientation.Rotate270CW;
                default:
                    return MyExifOrientation.HorizontalNormal;
            }
        }

        public static Orientation ToExifLibraryOrientation(this MyExifOrientation orientation)
        {
            switch (orientation)
            {
                case MyExifOrientation.HorizontalNormal:
                    return Orientation.Normal;
                case MyExifOrientation.Rotate180:
                    return Orientation.Rotated180;
                case MyExifOrientation.Rotate90CW:
                    return Orientation.RotatedLeft;
                case MyExifOrientation.Rotate270CW:
                    return Orientation.RotatedRight;
                default:
                    return Orientation.Normal;
            }
        }

        #endregion

        #region Conversion from/to System.Windows.Media.Imaging.Rotation

        public static MyExifOrientation ToMyExifOrientation(this Rotation rotation)
        {
            switch (rotation)
            {
                case Rotation.Rotate0:
                    return MyExifOrientation.HorizontalNormal;
                case Rotation.Rotate180:
                    return MyExifOrientation.Rotate180;
                case Rotation.Rotate90:
                    return MyExifOrientation.Rotate90CW;
                case Rotation.Rotate270:
                    return MyExifOrientation.Rotate270CW;
                default:
                    return MyExifOrientation.HorizontalNormal;
            }
        }

        public static Rotation ToRotation(this MyExifOrientation orientation)
        {
            switch (orientation)
            {
                case MyExifOrientation.HorizontalNormal:
                    return Rotation.Rotate0;
                case MyExifOrientation.Rotate180:
                    return Rotation.Rotate180;
                case MyExifOrientation.Rotate90CW:
                    return Rotation.Rotate90;
                case MyExifOrientation.Rotate270CW:
                    return Rotation.Rotate270;
                default:
                    return Rotation.Rotate0;
            }
        }

        #endregion

        #region Rotation

        public static MyExifOrientation Rotated(this MyExifOrientation orientation, string deg)
        {
            switch (deg)
            {
                case "90":
                    return orientation.Rotated90();
                case "180":
                    return orientation.Rotated180();
                case "270":
                    return orientation.Rotated270();
                default:
                    return orientation;
            }
        }

        public static MyExifOrientation Rotated90(this MyExifOrientation orientation)
        {
            switch (orientation)
            {
                case MyExifOrientation.HorizontalNormal:
                    return MyExifOrientation.Rotate90CW;
                case MyExifOrientation.Rotate180:
                    return MyExifOrientation.Rotate270CW;
                case MyExifOrientation.Rotate90CW:
                    return MyExifOrientation.Rotate180;
                case MyExifOrientation.Rotate270CW:
                    return MyExifOrientation.HorizontalNormal;
                default:
                    return MyExifOrientation.HorizontalNormal;
            }
        }

        public static MyExifOrientation Rotated180(this MyExifOrientation orientation)
        {
            switch (orientation)
            {
                case MyExifOrientation.HorizontalNormal:
                    return MyExifOrientation.Rotate180;
                case MyExifOrientation.Rotate180:
                    return MyExifOrientation.HorizontalNormal;
                case MyExifOrientation.Rotate90CW:
                    return MyExifOrientation.Rotate270CW;
                case MyExifOrientation.Rotate270CW:
                    return MyExifOrientation.Rotate90CW;
                default:
                    return MyExifOrientation.HorizontalNormal;
            }
        }

        public static MyExifOrientation Rotated270(this MyExifOrientation orientation)
        {
            switch (orientation)
            {
                case MyExifOrientation.HorizontalNormal:
                    return MyExifOrientation.Rotate270CW;
                case MyExifOrientation.Rotate180:
                    return MyExifOrientation.Rotate90CW;
                case MyExifOrientation.Rotate90CW:
                    return MyExifOrientation.HorizontalNormal;
                case MyExifOrientation.Rotate270CW:
                    return MyExifOrientation.Rotate180;
                default:
                    return MyExifOrientation.HorizontalNormal;
            }
        }

        #endregion
    }
}
