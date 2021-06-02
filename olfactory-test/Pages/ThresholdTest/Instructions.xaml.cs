using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Instructions : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Instructions()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);
        }


        // UI events

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
