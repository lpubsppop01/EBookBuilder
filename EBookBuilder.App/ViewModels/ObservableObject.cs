using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lpubsppop01.EBookBuilder.App.ViewModels;

/// <summary>The base for objects with change notification.</summary>
/// <remarks>Only what is needed is provided here, so that no MVVM toolkit has to be brought in.</remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Notifies only when the value has changed.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
