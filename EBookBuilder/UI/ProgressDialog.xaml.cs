using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressDialog : Window
    {
        #region Constructor

        public ProgressDialog()
        {
            InitializeComponent();
        }

        #endregion

        #region ShowDialog

        public void ShowDialog(Action<BackgroundWorker, DoWorkEventArgs> doWork)
        {
            var worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += (sender, e) => doWork(worker, e);
            worker.RunWorkerCompleted += (sender, e) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Send, (SendOrPostCallback)delegate
                {
                    Close();
                }, null);
            };
            worker.ProgressChanged += (sender, e) =>
            {
                string filename = (e.UserState is string) ? e.UserState as string : "";
                txtMessage.Text = filename;
            };
            worker.RunWorkerAsync();
            ShowDialog();
        }

        public static void ShowDialog(Action<BackgroundWorker, DoWorkEventArgs> doWork, Window owner)
        {
            var dialog = new ProgressDialog
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog(doWork);
        }

        #endregion
    }
}
