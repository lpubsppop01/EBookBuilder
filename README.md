# EBookBuilder

A tool to view scanned book page images side by side, rotate, crop and rename them to serial numbers, and write them out as a CBZ.

It was originally a Windows-only WPF (.NET Framework 4.8.1) application, and was ported to Avalonia UI + .NET 10 so that it runs on both Linux and Windows.
The version before the port can be taken from the `wpf-final` tag.

## Features

- **Rotation does not degrade image quality.** Page rotation is done by rewriting the EXIF orientation tag instead of rotating pixels.
  No re-encode occurs, so the image quality stays as it was no matter how many times a page is rotated.
- **Building is essentially lossless too.** When the size is left original, corner dots are off and the output is JPEG,
  the files are packaged into the CBZ byte for byte as they are (no re-encode).
- **Page order is reliable.** Pages are sorted in the ordinal order of their file names, and the CBZ entries are written in that order.
- Checking while looking at the preview, checking odd/even pages in bulk,
  and per-page duplication, move to end, deletion and cropping.
  Deletion and cropping apply to every checked page at once.
- When cropping, the range that will be removed is shown in red.
- The progress dialog can be cancelled.

## Requirements

- .NET 10 SDK
  - To install it yourself: `dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"`
  - When it is installed into `~/.dotnet`, `DOTNET_ROOT` and `PATH` must be set.
    Without them it stops at run time with "You must install .NET to run this application."
    ```sh
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
    ```

## Build and run

```sh
# GUI
dotnet run --project EBookBuilder.App

# Launch with a folder specified
dotnet run --project EBookBuilder.App -- ./scans

# CLI
dotnet run --project EBookBuilder.Cli -- --help

# Tests
dotnet test
```

To create a distribution:

```sh
dotnet publish EBookBuilder.App -c Release -r linux-x64 --self-contained   # Linux
dotnet publish EBookBuilder.App -c Release -r win-x64   --self-contained   # Windows
```

A self-contained build runs on machines where .NET is not installed.

## Command line

A CLI is included for doing the same work without opening the GUI.

```
ebookbuilder-cli list   --dir <folder>
ebookbuilder-cli rotate --dir <folder> --deg <90|180|270> [--index <n>]
ebookbuilder-cli crop   --dir <folder> [--index <n>] --left <px> --top <px> --right <px> --bottom <px>
ebookbuilder-cli rename --dir <folder>
ebookbuilder-cli move   --dir <folder> --index <n>
ebookbuilder-cli delete --dir <folder> --index <n>
ebookbuilder-cli build  --dir <folder> --out <output path>
                    [--format <jpeg|png>] [--size <original|width x height>] [--dots] [--quality <1-100>]
```

## Layout

| Project | Role |
| --- | --- |
| `EBookBuilder.Core` | Processing that does not depend on the UI. EXIF orientation, image processing, serial numbers, CBZ assembly |
| `EBookBuilder.App` | GUI built with Avalonia |
| `EBookBuilder.Cli` | Command line |
| `EBookBuilder.Core.Tests` | Tests for the core processing |
| `EBookBuilder.App.Tests` | Tests for screen state and operations (Avalonia headless run) |

Image processing uses SkiaSharp directly. Avalonia's `Bitmap` cannot save JPEG with a specified quality,
so SkiaSharp is needed to handle the re-encode quality.

## Differences from the original WPF version

Deliberate differences from the original WPF version.

- **No re-encode when building at the original size.** The original always re-encoded at JPEG quality 75 even when
  the original size was specified, and the image quality dropped with every build. It is now copied.
- **The JPEG quality can be specified.** The default is 90 (the original was fixed at 75 and did not expose it in the UI either).
  When the settings say not to re-encode, however, specifying it does not change the result, so the input field is disabled.
- **Build targets the whole folder.** It can be run regardless of how many pages are checked (the original did not require a single selection either).
- **Deletion and cropping apply to every checked page.** The original required exactly one checked page for both.
  Duplication and move to end still work on a single page, because their target position has to be unambiguous.
  Cropping opens the dialog only when all of the targets are of the same size, since the margins are chosen on a
  single preview image. Sizes that differ by up to 4 pixels or 1.5% (whichever is larger) are treated as the same,
  because the paper edge a scan detects moves from page to page; a page of a different format is still reported and
  stops the operation without changing anything. That report groups the pages by size, names the pages of the smaller
  groups and gives how far each size is from the rest in pixels and percent, so that the tolerance can be judged
  against what was actually scanned.
- **No upscaling when a size is specified.** When the target frame is larger than the original image, the original size is kept.
  The original upscaled. For the purpose of shrinking scanned images for e-books, upscaling is meaningless.
- **Page order does not depend on the file system.** The original used the order returned by `Directory.EnumerateFiles`
  as it was, implicitly depending on NTFS returning names in order.
  On file systems such as ext4 the order is undefined and pages get swapped. The pages are now sorted explicitly.
- **The CBZ entry order is fixed to the page order.** The original built the archive by walking the directory, so
  the order inside the archive was undefined. Readers that read in archive order swap the pages.
- **Settings are remembered.** In addition to the window position, the build settings and the last opened folder are saved.
  They are saved to `~/.config/EBookBuilder/settings.json` on Linux and to `%APPDATA%` on Windows.
- **The progress dialog does not hide failures.** The original swallowed exceptions during processing, so
  a failure did nothing and the cause was unknown. The contents are now shown.
- **Operations can be cancelled.** The original could not close the progress dialog.
- **Cropping works.** The original crop created its temporary file with `Path.GetTempFileName()`, so
  the extension became `.tmp` and saving always threw as an unsupported format
  (and, with the swallowing above, failed silently). A temporary file that keeps the original extension is now used.

## Known limitations

- **Avalonia does not support native Wayland on Linux.** It runs via X11 / XWayland.
  XWayland is present by default in most environments, so it works as is,
  but Wayland-specific benefits such as fractional scaling are not available.
- Rotation is expressed by the EXIF orientation tag, so **viewers that do not interpret EXIF do not reflect the rotation.**
  This has been the design since before the port. Most CBZ viewers interpret EXIF, but
  when using one that does not, the orientation has to be baked in from the start.

## Author

[lpubsppop01](https://github.com/lpubsppop01)

## License

[MIT License](https://github.com/lpubsppop01/EBookBuilder/raw/master/LICENSE.txt)

NuGet packages used:

| Reference | Version | License Type | License |
| --- | --- | --- | --- |
| Avalonia | 12.1.2 | MIT | https://licenses.nuget.org/MIT |
| SkiaSharp | 4.152.0 | MIT | https://licenses.nuget.org/MIT |
| ExifLibNet | 2.1.4 | MIT | https://licenses.nuget.org/MIT |
