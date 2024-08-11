using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lpubsppop01.EBookBuilder
{
    class JPEGFileItem : INotifyPropertyChanged
    {
        #region Properties

        string m_Filename = "";
        public string Filename
        {
            get { return m_Filename; }
            set { m_Filename = value; OnPropertyChanged(); }
        }

        bool m_IsChecked;
        public bool IsChecked
        {
            get { return m_IsChecked; }
            set { m_IsChecked = value; OnPropertyChanged(); }
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
