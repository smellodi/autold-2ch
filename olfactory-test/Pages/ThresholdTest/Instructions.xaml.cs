using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Instructions : Page, IPage
    {
        public event EventHandler Next = delegate { };

        public Instructions()
        {
            InitializeComponent();
        }


        // UI events

        private void OnNext_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
