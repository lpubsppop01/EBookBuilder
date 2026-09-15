using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;
using Lpubsppop01.EBookBuilder.Core.Pages;

namespace Lpubsppop01.EBookBuilder.Cli;

/// <summary>Implementation of each subcommand.</summary>
public static class Commands
{
    /// <summary>Lists the pages in sort order.</summary>
    public static Task<int> List(ArgumentReader args)
    {
        args.RejectUnknown("dir");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        if (filenames.Count == 0)
        {
            Console.WriteLine("No pages found.");
            return Task.FromResult(0);
        }

        foreach (var (filename, index) in filenames.Select((f, i) => (f, i)))
        {
            var path = Path.Combine(directoryPath, filename);
            var size = PageImagePipeline.ReadOrientedSize(path);
            var orientation = ExifOrientationStore.Read(path);
            Console.WriteLine($"{index,4}  {filename,-20} {size.Width,5} x {size.Height,-5} {Describe(orientation)}");
        }

        Console.WriteLine($"{filenames.Count} pages in total.");
        Console.WriteLine(PageNaming.AreSerialNumbers(filenames)
            ? "The file names are serial numbers."
            : "The file names are not serial numbers (rename is required before build).");
        return Task.FromResult(0);
    }

    static string Describe(ExifOrientation orientation) => orientation switch
    {
        ExifOrientation.HorizontalNormal => "-",
        ExifOrientation.Rotate90CW => "rotated 90",
        ExifOrientation.Rotate180 => "rotated 180",
        ExifOrientation.Rotate270CW => "rotated 270",
        _ => "unknown",
    };

    /// <summary>
    /// Rotates pages. The EXIF orientation tag is rewritten rather than the pixels, so the image quality does not degrade.
    /// </summary>
    public static async Task<int> Rotate(ArgumentReader args)
    {
        args.RejectUnknown("dir", "deg", "index");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var amount = args.RequireValue("deg") switch
        {
            "90" => RotationAmount.Deg90,
            "180" => RotationAmount.Deg180,
            "270" => RotationAmount.Deg270,
            var other => throw new ArgumentException($"--deg must be one of 90 / 180 / 270: {other}"),
        };

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        if (filenames.Count == 0)
        {
            Console.WriteLine("No pages found.");
            return 0;
        }

        var targets = SelectTargets(args, filenames);
        var progress = new ConsoleProgress("Rotated");
        await PageOperations.RotateAllAsync(directoryPath, targets, amount, progress);
        progress.Complete();

        Console.WriteLine($"Rotated {targets.Count} pages (orientation tag rewritten only).");
        return 0;
    }

    /// <summary>Crops pages by the given margins.</summary>
    public static Task<int> Crop(ArgumentReader args)
    {
        args.RejectUnknown("dir", "index", "left", "top", "right", "bottom", "quality");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        var targets = SelectTargets(args, filenames);

        var left = args.GetInt("left", 0);
        var top = args.GetInt("top", 0);
        var right = args.GetInt("right", 0);
        var bottom = args.GetInt("bottom", 0);
        var quality = args.GetInt("quality", PageImagePipeline.DefaultJpegQuality);

        if (left == 0 && top == 0 && right == 0 && bottom == 0)
            throw new ArgumentException("Specify at least one margin to remove.");

        foreach (var filename in targets)
        {
            PageOperations.Crop(directoryPath, filename, left, top, right, bottom, quality);
            var size = PageImagePipeline.ReadOrientedSize(Path.Combine(directoryPath, filename));
            Console.WriteLine($"{filename} → {size.Width} x {size.Height}");
        }

        Console.WriteLine($"Cropped {targets.Count} pages.");
        return Task.FromResult(0);
    }

    /// <summary>Renames files to serial numbers.</summary>
    public static Task<int> Rename(ArgumentReader args)
    {
        args.RejectUnknown("dir");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        if (filenames.Count == 0)
        {
            Console.WriteLine("No pages found.");
            return Task.FromResult(0);
        }

        var progress = new ConsoleProgress("Renamed");
        var result = PageOperations.RenameWithSerialNumbers(directoryPath, filenames, progress);
        progress.Complete();

        Console.WriteLine($"Renamed {result.Count} pages to serial numbers ({result[0]} to {result[^1]}).");
        return Task.FromResult(0);
    }

    /// <summary>Moves a page to the end.</summary>
    public static Task<int> MoveToLast(ArgumentReader args)
    {
        args.RejectUnknown("dir", "index");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        var targets = SelectTargets(args, filenames);

        var progress = new ConsoleProgress("Moved");
        PageOperations.MoveToLast(directoryPath, filenames, filenames.ToList().IndexOf(targets[0]), progress);
        progress.Complete();

        Console.WriteLine($"Moved {targets[0]} to the end.");
        return Task.FromResult(0);
    }

    /// <summary>Deletes pages. The serial numbers are not closed up.</summary>
    public static Task<int> Delete(ArgumentReader args)
    {
        args.RejectUnknown("dir", "index");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        var targets = SelectTargets(args, filenames);

        foreach (var filename in targets)
        {
            PageOperations.Delete(directoryPath, filename);
            Console.WriteLine($"Deleted: {filename}");
        }

        Console.WriteLine("The serial numbers have not been closed up. Run rename if needed.");
        return Task.FromResult(0);
    }

    /// <summary>Writes a CBZ file.</summary>
    public static async Task<int> Build(ArgumentReader args)
    {
        args.RejectUnknown("dir", "out", "format", "size", "dots", "quality");
        var directoryPath = args.RequireValue("dir");
        RequireDirectory(directoryPath);

        var outputFilePath = args.RequireValue("out");
        var format = args.GetValue("format")?.ToLowerInvariant() switch
        {
            null or "jpeg" or "jpg" => BuildImageFormatKind.Jpeg,
            "png" => BuildImageFormatKind.Png,
            var other => throw new ArgumentException($"--format must be one of jpeg / png: {other}"),
        };

        var (sizeKind, targetSize) = ParseSize(args.GetValue("size"));

        var options = new BuildOptions
        {
            OutputFilePath = outputFilePath,
            ImageFormatKind = format,
            SizeKind = sizeKind,
            TargetSize = targetSize,
            DrawsCornerDots = args.HasFlag("dots"),
            JpegQuality = args.GetInt("quality", PageImagePipeline.DefaultJpegQuality),
        };

        var filenames = PageFolder.EnumeratePageFilenames(directoryPath);
        if (filenames.Count == 0)
        {
            Console.WriteLine("No pages found.");
            return 1;
        }

        var progress = new ConsoleProgress("Processing");
        var result = await CbzBuilder.BuildAsync(directoryPath, filenames, options, progress);
        progress.Complete();

        Console.WriteLine($"Created {result.OutputFilePath}.");
        Console.WriteLine($"  {result.PageCount} pages (lossless copies {result.CopiedCount} / re-encoded {result.ReEncodedCount})");
        Console.WriteLine($"  {new FileInfo(result.OutputFilePath).Length / 1024:N0} KB");
        return 0;
    }

    static (BuildSizeKind, ImageSize) ParseSize(string? text)
    {
        if (text is null or "original") return (BuildSizeKind.Original, new ImageSize(600, 1024));

        var parts = text.Split('x', 'X');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
            throw new ArgumentException($"--size must be original or width x height (for example: 600x1024): {text}");
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"--size dimensions must be positive numbers: {text}");

        return (BuildSizeKind.Specified, new ImageSize(width, height));
    }

    static void RequireDirectory(string directoryPath)
    {
        if (!PageFolder.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Folder not found: {directoryPath}");
    }

    /// <summary>Targets the single page given by <c>--index</c> when present, otherwise every page.</summary>
    static IReadOnlyList<string> SelectTargets(ArgumentReader args, IReadOnlyList<string> filenames)
    {
        if (filenames.Count == 0) throw new InvalidOperationException("No pages found.");

        if (args.GetValue("index") is null) return filenames;

        var index = args.RequireInt("index");
        if (index < 0 || index >= filenames.Count)
            throw new ArgumentOutOfRangeException(nameof(args), $"Page number is out of range (0 to {filenames.Count - 1}): {index}");

        return [filenames[index]];
    }
}
