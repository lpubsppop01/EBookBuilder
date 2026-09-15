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

    /// <summary>Whether any area remains after cropping.</summary>
    public bool IsValid => CropWidth > 0 && CropHeight > 0;

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
        OnPropertyChanged(nameof(IsValid));
    }
}
