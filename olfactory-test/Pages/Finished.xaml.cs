using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Olfactory.Pages
{
    public partial class Finished : Page, IPage<bool>, INotifyPropertyChanged
    {
        public event EventHandler<bool> Next;       // true: exit, false: return to the fornt page
        public event EventHandler RequestSaving;
        public event PropertyChangedEventHandler PropertyChanged;

        public string TestName
        {
            get => _testName;
            set
            {
                _testName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestName)));
            }
        }

        public Finished()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            DataContext = this;
        }

        public void DisableSaving()
        {
            btnSaveData.IsEnabled = false;
        }

        // Internal

        string _testName = "";

        // UI events

        private void Page_GotFocus(object sender, RoutedEventArgs e)
        {
            btnSaveData.IsEnabled = true;
        }

        private void OnSaveData_Click(object sender, RoutedEventArgs e)
        {
            RequestSaving?.Invoke(this, new EventArgs());
        }

        private void OnReturn_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, false);
        }

        private void OnExit_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, true);
        }
    }
}
