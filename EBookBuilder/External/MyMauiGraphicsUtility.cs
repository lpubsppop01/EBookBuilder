using Microsoft.Maui.Graphics.Platform;

namespace Lpubsppop01.EBookBuilder;

public static class MyMauiGraphicsUtility
{
    public static byte[] GetBytes(string inputFilePath)
    {
        using var stream = File.OpenRead(inputFilePath);
        using var image = PlatformImage.FromStream(stream);
        var bytes = image.AsBytes();
        return bytes;
    }

    public static void Resize(string inputFilePath, string outputFilePath, int width, int height)
    {
        using var stream = File.OpenRead(inputFilePath);
        using var image = PlatformImage.FromStream(stream);
        var resizedImage = image.Resize(width, height);
        using var outputStream = File.Create(outputFilePath);
        resizedImage.Save(outputStream);
    }
}