using System.Diagnostics;

namespace lpubsppop01.EBookBuilder
{
    class ImageMagick
    {
        #region Instance

        static ImageMagick m_Current;
        public static ImageMagick Current
        {
            get
            {
                if (m_Current == null)
                {
                    m_Current = new ImageMagick();
                }
                return m_Current;
            }
        }

        #endregion

        #region Properties

        string m_MagickExe;
        string MagickExePath
        {
            get
            {
                if (m_MagickExe == null)
                {
                    m_MagickExe = CommandFinder.Find("magick.exe");
                }
                return m_MagickExe;
            }
        }

        public bool IsEnabled
        {
            get { return !string.IsNullOrEmpty(MagickExePath); }
        }

        #endregion

        #region Execute

        void Execute(string args)
        {
            var proc = new Process();
            proc.StartInfo.FileName = MagickExePath;
            proc.StartInfo.Arguments = args;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            proc.WaitForExit();
            proc.Close();
        }

        public void Resize(string inputFilePath, string outputFilePath, string size)
        {
            Execute(string.Format("\"{1}\" -resize {0} -gravity center -background white -extent {0} \"{2}\"", size, inputFilePath, outputFilePath));
        }

        #endregion
    }
}
