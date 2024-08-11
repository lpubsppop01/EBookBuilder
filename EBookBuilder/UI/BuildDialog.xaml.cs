namespace Lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for BuildDialog.xaml
    /// </summary>
    public partial class BuildDialog : ContentPage
    {
        #region Constructor

        public BuildDialog()
        {
            InitializeComponent();
        }

        #endregion

        #region Properties

        public Action? OnOK { get; set; }

        #endregion

        #region Event Handlers

        void OKButtonClicked(object sender, EventArgs e)
        {
            OnOK?.Invoke();
        }

        void CancelButtonClicked(object sender, EventArgs e)
        {
            Navigation.PopModalAsync();
        }

        #endregion
    }
}
