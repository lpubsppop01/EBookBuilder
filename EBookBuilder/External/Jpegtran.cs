using System.Diagnostics;

namespace lpubsppop01.EBookBuilder
{
    class JpegTran
    {
        #region Instance

        static JpegTran m_Current;
        public static JpegTran Current
        {
            get
            {
                if (m_Current == null)
                {
                    m_Current = new JpegTran();
                }
                return m_Current;
            }
        }

        #endregion

        #region Properties

        string m_JpegTranExe;
        string JpegTranExePath
        {
            get
            {
                if (m_JpegTranExe == null)
                {
                    m_JpegTranExe = CommandFinder.Find("jpegtran.exe");
                }
                return m_JpegTranExe;
            }
        }

        public bool IsEnabled
        {
            get { return !string.IsNullOrEmpty(JpegTranExePath); }
        }

        #endregion

        #region Execute

        void Execute(string args)
        {
            var proc = new Process();
            proc.StartInfo.FileName = JpegTranExePath;
            proc.StartInfo.Arguments = args;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            proc.WaitForExit();
            proc.Close();
        }

        public void Rotate(string inputFilePath, string outputFilePath, string rotDeg)
        {
            Execute(string.Format("-rotate {0} -copy all \"{1}\" \"{2}\"", rotDeg, inputFilePath, outputFilePath));
        }

        public void Crop(string inputFilePath, string outputFilePath, int width, int height, int left, int top)
        {
            Execute(string.Format("-crop {0}x{1}+{2}+{3} -copy all \"{4}\" \"{5}\"", width, height, left, top, inputFilePath, outputFilePath));
        }

        #endregion
    }
}
