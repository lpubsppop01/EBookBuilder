using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace lpubsppop01.EBookBuilder
{
    /// <summary>
    /// Interaction logic for CropDialog.xaml
    /// </summary>
    partial class CropDialog : Window
    {
        #region Constructor

        public CropDialog()
        {
            InitializeComponent();
        }

        #endregion

        #region Properties

        public new CropWorkData DataContext
        {
            get { return base.DataContext as CropWorkData; }
            set
            {
                if (DataContext != null) DataContext.PropertyChanged -= WorkData_PropertyChanged;
                base.DataContext = value;
                if (DataContext != null) DataContext.PropertyChanged += WorkData_PropertyChanged;
            }
        }

        #endregion

        #region Event Handlers

        void WorkData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (new[] { "Top", "Bottom", "Left", "Right" }.Contains(e.PropertyName))
            {
                UpdateMaskShape();
            }
        }

        void ctrlMaskShape_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMaskShape();
        }

        void UpdateMaskShape()
        {
            double sizeRatio = ctrlMaskShape.ActualHeight / DataContext.SourceImage.PixelHeight;
            double x = DataContext.Left * sizeRatio;
            double y = DataContext.Top * sizeRatio;
            double width = ctrlMaskShape.ActualWidth - (DataContext.Left + DataContext.Right) * sizeRatio;
            double height = ctrlMaskShape.ActualHeight - (DataContext.Top + DataContext.Bottom) * sizeRatio;
            ctrlMaskShape.Clip = new CombinedGeometry(
                new RectangleGeometry(new Rect(0, 0, ctrlMaskShape.ActualWidth, ctrlMaskShape.ActualHeight)),
                new RectangleGeometry(new Rect(x, y, width, height)))
            {
                GeometryCombineMode = GeometryCombineMode.Exclude
            };
        }

        void btnTopPlus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Top += 100;
        }

        void btnTopPlus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Top += 1;
        }

        void btnTopMinus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Top = Math.Max(0, DataContext.Top - 1);
        }

        void btnTopMinus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Top = Math.Max(0, DataContext.Top - 100);
        }

        void btnBottomPlus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Bottom += 100;
        }

        void btnBottomPlus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Bottom += 1;
        }

        void btnBottomMinus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Bottom = Math.Max(0, DataContext.Bottom - 1);
        }

        void btnBottomMinus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Bottom = Math.Max(0, DataContext.Bottom - 100);
        }

        void btnLeftPlus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Left += 100;
        }

        void btnLeftPlus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Left += 1;
        }

        void btnLeftMinus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Left = Math.Max(0, DataContext.Left - 1);
        }

        void btnLeftMinus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Left = Math.Max(0, DataContext.Left - 100);
        }

        void btnRightPlus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Right += 100;
        }

        void btnRightPlus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Right += 1;
        }

        void btnRightMinus1px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Right = Math.Max(0, DataContext.Right - 1);
        }

        void btnRightMinus100px_Click(object sender, RoutedEventArgs e)
        {
            DataContext.Right = Math.Max(0, DataContext.Right - 100);
        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        #endregion
    }
}
