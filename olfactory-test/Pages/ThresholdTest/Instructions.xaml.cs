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
            if (Storage.Instance.IsDebugging) lblDebug.Visibility = Visibility.Visible;
        }


        // UI events

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
