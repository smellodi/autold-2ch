using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages
{
    /// <summary>
    /// Interaction logic for Finished.xaml
    /// </summary>
    public partial class Finished : Page, IPage
    {
        public event EventHandler Next = delegate { };

        public Finished()
        {
            InitializeComponent();
        }

        private void OnExit_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
