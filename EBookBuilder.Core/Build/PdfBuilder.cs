using System.Text;
using Lpubsppop01.EBookBuilder.Core.Build.Pdf;
using Lpubsppop01.EBookBuilder.Core.Exif;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.Core.Build;

/// <summary>
/// Packages page images together and writes out a PDF.
/// </summary>
/// <remarks>
/// <para>
/// Every page enters the PDF as the JPEG it already is, tagged <c>/DCTDecode</c>, which is what
/// makes this lossless and keeps the file about the size of the equivalent CBZ. Re-encoding into
/// raw samples would make it an order of magnitude larger for no gain, since the pages are already
/// JPEG.
/// </para>
/// <para>
/// Rotation is written as the placement of the page rather than into the pixels, so a rotated page
/// is not re-encoded either. That mirrors how rotation is handled everywhere else in this program,
/// by rewriting the EXIF tag instead of the pixels.
/// </para>
/// <para>
/// The file is written in one ascending pass over the object numbers, so it can go straight to a
/// forward-only stream and never has to be seeked back into.
/// </para>
/// </remarks>
public static class PdfBuilder
{
    /// <summary>
    /// Writes out the pages and creates a PDF. An existing output file is overwritten.
    /// </summary>
    /// <exception cref="ArgumentException">When the settings cannot be built.</exception>
    /// <exception cref="InvalidOperationException">When the filenames are not serial numbers.</exception>
    public static async Task<BuildResult> BuildAsync(
        string directoryPath,
        IReadOnlyList<string> filenames,
        BuildOptions options,
        IProgress<PageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        // A page that cannot be stored as it is falls back to the re-encoding branch while staging,
        // so this pass only has to write out what it is handed.
        using var staged = await PageStager.CreateAsync(
            directoryPath,
            filenames,
            options,
            canUseVerbatim: JpegHeader.IsEmbeddable,
            progress: progress,
            cancellationToken: cancellationToken);

        var outputDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(options.OutputFilePath));
        if (!string.IsNullOrEmpty(outputDirectoryPath)) Directory.CreateDirectory(outputDirectoryPath);

        if (File.Exists(options.OutputFilePath)) File.Delete(options.OutputFilePath);

        using (var stream = File.Create(options.OutputFilePath))
        {
            WriteDocument(stream, staged, options, cancellationToken);
        }

        return new BuildResult(options.OutputFilePath, filenames.Count, staged.CopiedCount);
    }

    /// <summary>Writes the whole file. The packaging is sequential, as a ZIP's is.</summary>
    static void WriteDocument(Stream stream, PageStager staged, BuildOptions options, CancellationToken cancellationToken)
    {
        var writer = new PdfWriter(stream);
        writer.WriteHeader();

        var catalogId = writer.AllocateObjectId();
        var pagesId = writer.AllocateObjectId();

        // Every number is taken before anything is written, so the objects come out in ascending
        // order and the page can name the image and content objects that follow it.
        var pageIds = new int[staged.Pages.Count];
        var imageIds = new int[staged.Pages.Count];
        var contentIds = new int[staged.Pages.Count];
        for (var i = 0; i < staged.Pages.Count; ++i)
        {
            pageIds[i] = writer.AllocateObjectId();
            imageIds[i] = writer.AllocateObjectId();
            contentIds[i] = writer.AllocateObjectId();
        }

        writer.WriteObject(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");

        var kids = string.Join(' ', pageIds.Select(id => $"{id} 0 R"));
        writer.WriteObject(pagesId, $"<< /Type /Pages /Kids [ {kids} ] /Count {staged.Pages.Count} >>");

        for (var i = 0; i < staged.Pages.Count; ++i)
        {
            // Reading and writing the pages of a long book takes long enough to be worth stopping.
            cancellationToken.ThrowIfCancellationRequested();
            WritePage(writer, staged, i, pagesId, pageIds[i], imageIds[i], contentIds[i], options);
        }

        writer.WriteXrefAndTrailer(catalogId);
    }

    /// <summary>Writes the three objects one page is made of: the page, its image and its content.</summary>
    static void WritePage(
        PdfWriter writer,
        PageStager staged,
        int index,
        int pagesId,
        int pageId,
        int imageId,
        int contentId,
        BuildOptions options)
    {
        var page = staged.Pages[index];
        var path = staged.PathOf(page.Name);
        var bytes = File.ReadAllBytes(path);

        // The image XObject declares the image's own pixel dimensions, before any rotation.
        var codedSize = PageImagePipeline.ReadEncodedSize(path);

        // A page that was re-encoded had its rotation baked into the pixels while staging, so the
        // placement must not turn it a second time. A copied page still carries its tag.
        var orientation = page.Copied ? ExifOrientationStore.Read(path) : ExifOrientation.HorizontalNormal;
        var placement = PdfPagePlacement.For(codedSize, orientation, options.PdfPageDpi);

        // The colour space has to match the JPEG's own component count, or the page renders wrong.
        var colorSpace = JpegHeader.ReadComponentCount(path) == 1 ? "/DeviceGray" : "/DeviceRGB";

        var mediaBox = string.Join(' ',
            "[ 0 0", PdfWriter.Format(placement.PageWidth), PdfWriter.Format(placement.PageHeight), "]");

        writer.WriteObject(
            pageId,
            $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox {mediaBox} "
            + $"/Resources << /XObject << /{PdfPagePlacement.ResourceName} {imageId} 0 R >> >> "
            + $"/Contents {contentId} 0 R >>");

        writer.WriteStreamObject(
            imageId,
            $"/Type /XObject /Subtype /Image /Width {codedSize.Width} /Height {codedSize.Height} "
            + $"/ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /DCTDecode",
            bytes);

        writer.WriteStreamObject(contentId, "", Encoding.ASCII.GetBytes(placement.ToContentStream()));
    }
}
