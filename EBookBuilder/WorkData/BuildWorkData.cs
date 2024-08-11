using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lpubsppop01.EBookBuilder
{
    class BuildWorkData : INotifyPropertyChanged
    {
        #region Instance

        BuildWorkData()
        {
        }

        static BuildWorkData? m_Current;
        public static BuildWorkData Current
        {
            get
            {
                if (m_Current == null)
                {
                    m_Current = new BuildWorkData();
                }
                return m_Current;
            }
        }

        #endregion

        #region Properties

        int m_Width = 600;
        public int Width
        {
            get { return m_Width; }
            set { m_Width = value; OnPropertyChanged(); }
        }

        int m_Height = 1024;
        public int Height
        {
            get { return m_Height; }
            set { m_Height = value; OnPropertyChanged(); }
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
