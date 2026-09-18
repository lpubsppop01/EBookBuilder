using Lpubsppop01.EBookBuilder.App.Services;

namespace Lpubsppop01.EBookBuilder.App.Tests;

/// <summary>
/// Verification of what the save picker offers.
/// </summary>
/// <remarks>
/// The picker itself needs a live window and cannot be tested here, so the part that decides what
/// it should say is kept apart from it and tested on its own. Offering to save a PDF as a
/// <c>.cbz</c> would be a wrong statement about the file being written.
/// </remarks>
public class DialogServiceTests
{
    [Fact]
    public void APdfNameOffersThePdfFileType()
    {
        var (title, types) = DialogService.DescribeSaveTarget("/tmp/book.pdf");

        Assert.Contains("PDF", title);
        Assert.Equal(["*.pdf"], types.Single().Patterns!);
    }

    [Fact]
    public void ACbzNameOffersBothZipFileTypes()
    {
        var (title, types) = DialogService.DescribeSaveTarget("/tmp/book.cbz");

        Assert.Contains("CBZ", title);
        Assert.Equal(["*.cbz", "*.zip"], types.Select(type => type.Patterns!.Single()));
    }

    [Fact]
    public void TheExtensionIsReadWithoutRegardToCase()
    {
        var (title, _) = DialogService.DescribeSaveTarget("/tmp/BOOK.PDF");
        Assert.Contains("PDF", title);
    }

    /// <summary>A name with no extension yet is one on its way to a CBZ, which is the default.</summary>
    [Fact]
    public void ANameWithoutAnExtensionOffersTheCbzFileType()
    {
        var (title, _) = DialogService.DescribeSaveTarget("/tmp/book");
        Assert.Contains("CBZ", title);
    }
}
