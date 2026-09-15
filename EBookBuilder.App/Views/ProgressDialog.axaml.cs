using Avalonia.Controls;
using Avalonia.Interactivity;
using Lpubsppop01.EBookBuilder.Core;

namespace Lpubsppop01.EBookBuilder.App.Views;

/// <summary>
/// The dialog that shows the progress of time-consuming work.
/// </summary>
/// <remarks>
/// <para>
/// The original WPF version used <c>BackgroundWorker</c> and threw away exceptions from the
/// work in <c>RunWorkerCompleted</c>. As a result nothing happened on failure and the cause
/// was unknown (this is why cropping did not work).
/// </para>
/// <para>
/// Here the exception is kept in <see cref="Error"/> so that the caller can tell the user about it.
/// The Stop button cancels the <see cref="CancellationTokenSource"/>.
/// </para>
/// </remarks>
public partial class ProgressDialog : Window
{
    readonly CancellationTokenSource m_Cancellation = new();
    Func<IProgress<PageProgress>, CancellationToken, Task>? m_Work;

    /// <summary>The exception that occurred during the work. Null on success.</summary>
    public Exception? Error { get; private set; }

    /// <summary>True if the work was cancelled.</summary>
    public bool WasCancelled { get; private set; }

    public ProgressDialog()
    {
        InitializeComponent();
    }

    public ProgressDialog(string label, Func<IProgress<PageProgress>, CancellationToken, Task>? work) : this()
    {
        m_Work = work;
        ctrlMessage.Text = label;
    }

    // Do not write InitializeComponent by hand.
    // The XAML compiler generates a version that assigns the x:Name fields,
    // and writing one here makes overload resolution prefer it, leaving the fields null.

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = RunAsync();
    }

    async Task RunAsync()
    {
        if (m_Work is null)
        {
            Close();
            return;
        }

        // Created on the UI thread, so Report is called on the UI thread
        var progress = new Progress<PageProgress>(OnProgress);

        try
        {
            await Task.Run(() => m_Work(progress, m_Cancellation.Token), m_Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            Close();
        }
    }

    void OnProgress(PageProgress value)
    {
        ctrlProgress.Value = value.Percentage;
        ctrlMessage.Text = $"{value.Done} / {value.Total} ({value.Percentage}%)";
    }

    void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ctrlCancel.IsEnabled = false;
        m_Cancellation.Cancel();
    }
}
