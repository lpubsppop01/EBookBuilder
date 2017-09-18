using System.IO;
using System.Windows;

namespace lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length == 0) return;
            if (Directory.Exists(e.Args[0]))
            {
                MainWorkData.Current.TargetDirectoryPath = e.Args[0];
            }
        }
    }
}
