using System.Windows.Input;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>A command whose executability can be re-queried.</summary>
public interface IRelayCommand : ICommand
{
    /// <summary>Signals that the executability may have changed.</summary>
    void RaiseCanExecuteChanged();
}

/// <summary>Turns asynchronous work into a command. Prevents double execution while it runs.</summary>
/// <remarks>
/// In the original WPF version the buttons could still be pressed while the progress dialog
/// was shown, so the same work could be run in parallel. Here it is disabled while running.
/// </remarks>
public sealed class AsyncRelayCommand : IRelayCommand
{
    readonly Func<Task> m_Execute;
    readonly Func<bool>? m_CanExecute;
    bool m_IsRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        m_Execute = execute;
        m_CanExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !m_IsRunning && (m_CanExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        m_IsRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await m_Execute();
        }
        finally
        {
            m_IsRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
