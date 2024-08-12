using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Lpubsppop01.EBookBuilder
{
    class MainWorkData : INotifyPropertyChanged
    {
        #region Instance

        MainWorkData()
        {
            JPEGFileItems.CollectionChanged += JPEGFileItems_CollectionChanged;
        }

        static MainWorkData? m_Current;
        public static MainWorkData Current
        {
            get
            {
                if (m_Current == null)
                {
                    m_Current = new MainWorkData();
                }
                return m_Current;
            }
        }

        #endregion

        #region Properties

        string m_TargetDirectoryPath = "";
        public string TargetDirectoryPath
        {
            get { return m_TargetDirectoryPath; }
            set { m_TargetDirectoryPath = value; OnPropertyChanged(); }
        }

        public MyObservableCollection<JPEGFileItem> JPEGFileItems { get; private set; }
            = new MyObservableCollection<JPEGFileItem>();

        #endregion

        #region Load

        public void LoadJPEGFileItems()
        {
            JPEGFileItems.Clear();
            if (!Directory.Exists(TargetDirectoryPath)) return;
            var filePaths = new DirectoryInfo(TargetDirectoryPath).GetFiles()
                .OrderBy(fileInfo => Path.GetFileNameWithoutExtension(fileInfo.Name))
                .Select(fileInfo => fileInfo.FullName);
            var itemsToAdd = new List<JPEGFileItem>();
            foreach (var filePath in filePaths)
            {
                string filename = Path.GetFileName(filePath);
                if (!Regex.IsMatch(filename, @".*\.(jpg|jpeg)$", RegexOptions.IgnoreCase)) continue;
                itemsToAdd.Add(new JPEGFileItem { Filename = filename });
            }
            JPEGFileItems.AddRange(itemsToAdd);
        }

        #endregion

        #region Event Handlers

        void JPEGFileItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged += item_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged -= item_PropertyChanged;
                }
            }
        }

        void item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("JPEGFileItems");
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
