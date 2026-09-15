using Lpubsppop01.EBookBuilder.Cli;

if (args.Length == 0)
{
    PrintUsage(Console.Out);
    return 1;
}

if (args[0] is "-h" or "--help" or "help")
{
    PrintUsage(Console.Out);
    return 0;
}

var command = args[0];
var reader = ArgumentReader.Parse(args[1..]);

try
{
    return command switch
    {
        "list" => await Commands.List(reader),
        "rotate" => await Commands.Rotate(reader),
        "crop" => await Commands.Crop(reader),
        "rename" => await Commands.Rename(reader),
        "move" => await Commands.MoveToLast(reader),
        "delete" => await Commands.Delete(reader),
        "build" => await Commands.Build(reader),
        _ => Fail(command),
    };
}
catch (Exception ex) when (ex is ArgumentException
                             or DirectoryNotFoundException
                             or FileNotFoundException
                             or InvalidOperationException
                             or InvalidDataException
                             or IOException
                             or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int Fail(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintUsage(Console.Error);
    return 1;
}

static void PrintUsage(TextWriter writer)
{
    writer.WriteLine("""
        ebookbuilder-cli - build a CBZ from scanned page images

        Usage:
          ebookbuilder-cli <command> [options]

        Commands:
          list    List the pages in sort order
                  --dir <folder>

          rotate  Rotate pages (rewrites the EXIF orientation tag, so the image quality does not degrade)
                  --dir <folder> --deg <90|180|270> [--index <n>]
                  Omitting --index targets every page.

          crop    Crop pages by the given margins
                  --dir <folder> [--index <n>]
                  --left <px> --top <px> --right <px> --bottom <px> [--quality <1-100>]

          rename  Rename the files to serial numbers
                  --dir <folder>

          move    Move a page to the end and renumber the serial numbers
                  --dir <folder> --index <n>

          delete  Delete pages (the serial numbers are not closed up)
                  --dir <folder> --index <n>

          build   Write a CBZ file (the file names must be serial numbers)
                  --dir <folder> --out <output path>
                  [--format <jpeg|png>] [--size <original|width x height>]
                  [--dots] [--quality <1-100>]

                  When --size is original, --dots is absent and the output is jpeg,
                  the files are packaged as they are without re-encoding (the image quality stays as it was).
                  The default of --size is original. The default of --quality is 90.

        Examples:
          ebookbuilder-cli list   --dir ./scans
          ebookbuilder-cli rotate --dir ./scans --deg 90
          ebookbuilder-cli crop   --dir ./scans --index 3 --left 20 --right 20
          ebookbuilder-cli build  --dir ./scans --out ./book.cbz --size 600x1024 --dots
        """);
}
