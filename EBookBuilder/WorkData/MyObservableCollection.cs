using System.Collections.ObjectModel;
using System.Collections.Specialized;

class MyObservableCollection<T>: ObservableCollection<T> {
    public void AddRange(IEnumerable<T> items) {
        foreach (var item in items) {
            Items.Add(item);
        }
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}