using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Lpubsppop01.EBookBuilder.App.Imaging;
using Lpubsppop01.EBookBuilder.App.ViewModels;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.Views;

/// <summary>
/// The dialog for deciding the crop area.
/// </summary>
/// <remarks>
/// <para>
/// The original WPF version overlaid a single semi-transparent red rectangle and formed the
/// frame by setting its <c>Clip</c> to the difference of two rectangles.
/// Avalonia has no shape that handles set differences, so each of the four sides to remove
/// is covered by its own rectangle.
/// </para>
/// <para>
/// The margins are specified through numeric inputs instead of the original's sliders and
/// ±1 / ±100 buttons. It can do the same thing, and a wide range can be specified quickly.
/// </para>
/// </remarks>
public partial class CropDialog : Window
{
    /// <summary>The maximum preview size.</summary>
    static readonly ImageSize PreviewMaxSize = new(2000, 2000);

    CropSettings? m_Settings;

    public CropDialog()
    {
        InitializeComponent();
    }

    public CropDialog(CropSettings settings, string imagePath) : this()
    {
        m_Settings = settings;
        DataContext = settings;
        settings.PreviewImage = BitmapInterop.LoadPreview(imagePath, PreviewMaxSize);

        // Redraw the red frame when a margin changes
        settings.PropertyChanged += (_, _) => UpdateMask();

        // The displayed size of the image changes when the window is resized
        ctrlPreviewHost.SizeChanged += (_, _) => UpdateMask();
        Opened += (_, _) => UpdateMask();
    }

    // Do not write InitializeComponent by hand.
    // The XAML compiler generates a version that assigns the x:Name fields,
    // and writing one here makes overload resolution prefer it, leaving the fields null.

    /// <summary>Paints the four sides to be removed to match the rectangle where the image is actually drawn.</summary>
    void UpdateMask()
    {
        if (m_Settings is null) return;

        var source = m_Settings.SourceSize;
        if (source.Width <= 0 || source.Height <= 0) return;

        var hostWidth = ctrlPreviewHost.Bounds.Width;
        var hostHeight = ctrlPreviewHost.Bounds.Height;
        if (hostWidth <= 0 || hostHeight <= 0) return;

        // Work out the actual size drawn with Stretch="Uniform"
        var scale = Math.Min(hostWidth / source.Width, hostHeight / source.Height);
        var imageWidth = source.Width * scale;
        var imageHeight = source.Height * scale;

        var offsetX = (hostWidth - imageWidth) / 2;
        var offsetY = (hostHeight - imageHeight) / 2;

        var left = offsetX + (m_Settings.Left * scale);
        var top = offsetY + (m_Settings.Top * scale);
        var right = offsetX + imageWidth - (m_Settings.Right * scale);
        var bottom = offsetY + imageHeight - (m_Settings.Bottom * scale);

        var keptWidth = Math.Max(0, right - left);
        var keptHeight = Math.Max(0, bottom - top);

        SetRectangle(ctrlMaskTop, offsetX, offsetY, imageWidth, Math.Max(0, top - offsetY));
        SetRectangle(ctrlMaskBottom, offsetX, bottom, imageWidth, Math.Max(0, offsetY + imageHeight - bottom));
        SetRectangle(ctrlMaskLeft, offsetX, top, Math.Max(0, left - offsetX), keptHeight);
        SetRectangle(ctrlMaskRight, right, top, Math.Max(0, offsetX + imageWidth - right), keptHeight);
    }

    static void SetRectangle(Control rectangle, double x, double y, double width, double height)
    {
        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        rectangle.Width = width;
        rectangle.Height = height;
    }

    void Reset_Click(object? sender, RoutedEventArgs e)
    {
        m_Settings?.Reset();
        UpdateMask();
    }

    void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
