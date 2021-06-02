using System;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages
{
    public partial class Finished : Page, IPage<bool>
    {
        public event EventHandler<bool> Next = delegate { }; // true: exit, false: return to the fornt page

        public Finished()
        {
            InitializeComponent();
            Storage.Instance.Changed += (s, e) =>
            {
                if (e == Storage.Data.IsDebugging) lblDebug.Visibility = Visibility.Visible;
            };
        }


        // UI events

        private void OnReturn_Click(object sender, RoutedEventArgs e)
        {
            Next(this, false);
        }

        private void OnExit_Click(object sender, RoutedEventArgs e)
        {
            Next(this, true);
        }
    }
}
