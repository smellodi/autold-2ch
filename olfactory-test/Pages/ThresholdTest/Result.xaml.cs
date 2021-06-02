using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Result : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Result()
        {
            InitializeComponent();

            lblDebug.Visibility = Storage.Instance.IsDebugging ? Visibility.Visible : Visibility.Collapsed;

            var zoomLevelBinding = new Binding("ZoomLevel");
            zoomLevelBinding.Source = Storage.Instance;
            BindingOperations.SetBinding(sctScale, ScaleTransform.ScaleXProperty, zoomLevelBinding);
            BindingOperations.SetBinding(sctScale, ScaleTransform.ScaleYProperty, zoomLevelBinding);
        }

        public void SetPPM(double ppm)
        {
            lblContent.Content = ppm > 0 ? ppm.ToString("F2") : "unknown";
        }


        // UI events

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
