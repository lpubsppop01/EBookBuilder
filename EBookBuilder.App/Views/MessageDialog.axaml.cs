using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lpubsppop01.EBookBuilder.App.Views;

/// <summary>A small dialog for confirmations and notifications.</summary>
/// <remarks>Avalonia has no <c>MessageBox</c>, so one is provided here.</remarks>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, bool showsCancel) : this()
    {
        Title = title;
        ctrlMessage.Text = message;
        ctrlCancel.IsVisible = showsCancel;
    }

    // Do not write InitializeComponent by hand.
    // The XAML compiler generates a version that assigns the x:Name fields,
    // and writing one here makes overload resolution prefer it, leaving the fields null.

    void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
