using Avalonia.Media.Imaging;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>The contents of the crop dialog. Holds the margins to remove.</summary>
public sealed class CropSettings : ObservableObject
{
    int m_Top;
    int m_Bottom;
    int m_Left;
    int m_Right;
    ImageSize m_SourceSize;
    WriteableBitmap? m_PreviewImage;
    int m_TargetCount = 1;
    int m_SizeDifference;

    /// <summary>The preview image for verification.</summary>
    public WriteableBitmap? PreviewImage
    {
        get => m_PreviewImage;
        set => SetProperty(ref m_PreviewImage, value);
    }

    /// <summary>Height to remove from the top.</summary>
    public int Top
    {
        get => m_Top;
        set => SetMargin(ref m_Top, value);
    }

    /// <summary>Height to remove from the bottom.</summary>
    public int Bottom
    {
        get => m_Bottom;
        set => SetMargin(ref m_Bottom, value);
    }

    /// <summary>Width to remove from the left.</summary>
    public int Left
    {
        get => m_Left;
        set => SetMargin(ref m_Left, value);
    }

    /// <summary>Width to remove from the right.</summary>
    public int Right
    {
        get => m_Right;
        set => SetMargin(ref m_Right, value);
    }

    /// <summary>The dimensions of the source image (after applying the EXIF rotation).</summary>
    public ImageSize SourceSize
    {
        get => m_SourceSize;
        set
        {
            if (!SetProperty(ref m_SourceSize, value)) return;
            OnPropertyChanged(nameof(CropWidth));
            OnPropertyChanged(nameof(CropHeight));
        }
    }

    /// <summary>Width remaining after cropping.</summary>
    public int CropWidth => Math.Max(0, m_SourceSize.Width - m_Left - m_Right);

    /// <summary>Height remaining after cropping.</summary>
    public int CropHeight => Math.Max(0, m_SourceSize.Height - m_Top - m_Bottom);

    /// <summary>Whether an area remains after removing the margins from an image of the given size.</summary>
    /// <remarks>
    /// The margins have to be checked against every target, because the targets are not always
    /// exactly the same size (see <see cref="CropSizeTolerance"/> in the core library).
    /// </remarks>
    public bool LeavesAreaOn(ImageSize size) =>
        size.Width - (m_Left + m_Right) > 0 && size.Height - (m_Top + m_Bottom) > 0;

    /// <summary>The number of pages the same margins are applied to.</summary>
    /// <remarks>
    /// Only one page is shown in the preview, so when several are checked the dialog says so.
    /// </remarks>
    public int TargetCount
    {
        get => m_TargetCount;
        set
        {
            if (!SetProperty(ref m_TargetCount, value)) return;
            OnPropertyChanged(nameof(AppliesToSeveralPages));
            OnPropertyChanged(nameof(SeveralPagesNotice));
        }
    }

    /// <summary>The largest difference in pixels between the target sizes. 0 when they are all equal.</summary>
    public int SizeDifference
    {
        get => m_SizeDifference;
        set
        {
            if (!SetProperty(ref m_SizeDifference, value)) return;
            OnPropertyChanged(nameof(SeveralPagesNotice));
        }
    }

    /// <summary>Whether the margins are applied to more than one page.</summary>
    public bool AppliesToSeveralPages => m_TargetCount > 1;

    /// <summary>The notice shown when several pages are the target. Empty when there is only one.</summary>
    public string SeveralPagesNotice
    {
        get
        {
            if (!AppliesToSeveralPages) return "";

            var notice = $"The same margins will be applied to {m_TargetCount} pages."
                + " The preview shows the first one.";
            if (m_SizeDifference > 0)
            {
                notice += $" The pages differ by up to {m_SizeDifference} pixels in size.";
            }
            return notice;
        }
    }

    /// <summary>Resets all margins to 0.</summary>
    public void Reset()
    {
        Top = 0;
        Bottom = 0;
        Left = 0;
        Right = 0;
    }

    void SetMargin(ref int field, int value)
    {
        if (field == value) return;
        field = Math.Max(0, value);
        OnPropertyChanged();
        OnPropertyChanged(nameof(CropWidth));
        OnPropertyChanged(nameof(CropHeight));
    }
}
