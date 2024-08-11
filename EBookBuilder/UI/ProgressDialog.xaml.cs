using System;
using System.ComponentModel;
using System.Threading;

namespace Lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : ContentPage
    {
        #region Constructor

        public ProgressDialog()
        {
            InitializeComponent();
        }

        #endregion

        #region ShowDialog

        public static void ShowDialog(Action<BackgroundWorker, DoWorkEventArgs> doWork, ContentPage owner)
        {
            var dialog = new ProgressDialog();
            dialog.ShowDialogCore(doWork, owner);
        }

        void ShowDialogCore(Action<BackgroundWorker, DoWorkEventArgs> doWork, ContentPage owner)
        {
            var worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += (sender, e) => doWork(worker, e);
            worker.RunWorkerCompleted += async (sender, e) =>
            {
                await Navigation.PopModalAsync();
            };
            worker.ProgressChanged += (sender, e) =>
            {
                var filename = e.UserState as string ?? "";
                txtMessage.Text = filename;
            };
            worker.RunWorkerAsync();
            owner.Navigation.PushModalAsync(this);
        }

        #endregion
    }
}
