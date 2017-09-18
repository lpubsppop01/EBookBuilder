using Microsoft.Win32;
using System.Windows;

namespace lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for BuildDialog.xaml
    /// </summary>
    public partial class BuildDialog : Window
    {
        #region Constructor

        public BuildDialog()
        {
            InitializeComponent();
        }

        #endregion

        #region Event Handlers

        void btnSelectOutputFilePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.FileName = System.IO.Path.GetFileName(txtOutputFilePath.Text);
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(txtOutputFilePath.Text);
            dialog.Filter = "Zip Archive (.zip)|*.zip|Comic Book Zip (.cbz)|*.cbz";
            dialog.DefaultExt = ".cbz";
            if (dialog.ShowDialog() ?? false)
            {
                txtOutputFilePath.Text = dialog.FileName;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        #endregion
    }
}
