using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace lpubsppop01.EBookBuilder
{
    class CropWorkData : INotifyPropertyChanged
    {
        #region Instance

        CropWorkData()
        {
        }

        static CropWorkData m_Current;
        public static CropWorkData Current
        {
            get
            {
                if (m_Current == null)
                {
                    m_Current = new CropWorkData();
                }
                return m_Current;
            }
        }

        #endregion

        #region Properties

        int m_Top = 600;
        public int Top
        {
            get { return m_Top; }
            set { m_Top = value; OnPropertyChanged(); }
        }

        int m_Bottom = 0;
        public int Bottom
        {
            get { return m_Bottom; }
            set { m_Bottom = value; OnPropertyChanged(); }
        }

        int m_Left = 0;
        public int Left
        {
            get { return m_Left; }
            set { m_Left = value; OnPropertyChanged(); }
        }

        int m_Right = 0;
        public int Right
        {
            get { return m_Right; }
            set { m_Right = value; OnPropertyChanged(); }
        }

        BitmapImage m_PreviewImage;
        public BitmapImage PreviewImage
        {
            get { return m_PreviewImage; }
            set { m_PreviewImage = value; OnPropertyChanged(); }
        }

        BitmapImage m_SourceImage;
        public BitmapImage SourceImage
        {
            get { return m_SourceImage; }
            set { m_SourceImage = value; OnPropertyChanged(); }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
