using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Instructions : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Instructions()
        {
            InitializeComponent();

            lblDebug.Visibility = Storage.Instance.IsDebugging ? Visibility.Visible : Visibility.Collapsed;

            var zoomLevelBinding = new Binding("ZoomLevel");
            zoomLevelBinding.Source = Storage.Instance;
            BindingOperations.SetBinding(sctScale, ScaleTransform.ScaleXProperty, zoomLevelBinding);
            BindingOperations.SetBinding(sctScale, ScaleTransform.ScaleYProperty, zoomLevelBinding);
        }


        // UI events

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
