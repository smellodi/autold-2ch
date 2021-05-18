using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages
{
    public partial class Finished : Page, IPage<EventArgs>
    {
        public event EventHandler<EventArgs> Next = delegate { };

        public Finished()
        {
            InitializeComponent();
        }


        // UI events

        private void OnExit_Click(object sender, RoutedEventArgs e)
        {
            Next(this, new EventArgs());
        }
    }
}
