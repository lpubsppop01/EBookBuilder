using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace lpubsppop01.EBookBuilder
{
    enum BuildImageFormatKind
    {
        JPEG,
        PNG,
    }

    enum BuildSizeKind
    {
        Original,
        Specified,
    }

    class BuildWorkData : INotifyPropertyChanged
    {
        #region Instance

        BuildWorkData()
        {
        }

        static BuildWorkData m_Current;
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

        string m_OutputFilePath = "";
        public string OutputFilePath
        {
            get { return m_OutputFilePath; }
            set { m_OutputFilePath = value; OnPropertyChanged(); }
        }

        BuildImageFormatKind m_ImageFormatKind = BuildImageFormatKind.JPEG;
        public BuildImageFormatKind ImageFormatKind
        {
            get { return m_ImageFormatKind; }
            set { m_ImageFormatKind = value; OnPropertyChanged(); }
        }

        BuildSizeKind m_SizeKind = BuildSizeKind.Original;
        public BuildSizeKind SizeKind
        {
            get { return m_SizeKind; }
            set { m_SizeKind = value; OnPropertyChanged(); }
        }

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

        bool m_DrawsCornerDots = false;
        public bool DrawsCornerDots
        {
            get { return m_DrawsCornerDots; }
            set { m_DrawsCornerDots = value; OnPropertyChanged(); }
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
