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
            if (Storage.Instance.IsDebugging) lblDebug.Visibility = Visibility.Visible;
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
