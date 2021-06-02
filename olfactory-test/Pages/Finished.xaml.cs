using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Olfactory.Pages
{
    public partial class Finished : Page, IPage<bool>
    {
        public event EventHandler<bool> Next = delegate { }; // true: exit, false: return to the fornt page

        public Finished()
        {
            InitializeComponent();

            var isDebuggingBinding = new Binding("IsDebugging");
            isDebuggingBinding.Source = Storage.Instance;
            isDebuggingBinding.Converter = new BooleanToVisibilityConverter();
            BindingOperations.SetBinding(lblDebug, VisibilityProperty, isDebuggingBinding);
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
