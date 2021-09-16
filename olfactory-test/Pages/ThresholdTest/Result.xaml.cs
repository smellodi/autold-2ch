using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Result : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Result()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);
        }

        public void SetPPM(double ppm)
        {
            lblContent.Content = ppm > 0 ? ppm.ToString("F2") : Utils.L10n.T("Unknown");
        }


        // UI events

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
