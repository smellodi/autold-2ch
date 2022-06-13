using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Navigation = Olfactory.Tests.ThresholdTest.Navigation;
using ProcedureType = Olfactory.Tests.ThresholdTest.Settings.ProcedureType;

namespace Olfactory.Pages.ThresholdTest
{
    public partial class Instructions : Page, IPage<Navigation>, INotifyPropertyChanged
    {
        public event EventHandler<Navigation> Next;
        public event PropertyChangedEventHandler PropertyChanged;

        public string ProcedureInstruction => "ThTestInstr" + _procType.ToString();

        public Instructions()
        {
            InitializeComponent();

            Storage.Instance.BindScaleToZoomLevel(sctScale);
            Storage.Instance.BindVisibilityToDebug(lblDebug);

            DataContext = this;
        }

        public void Init(ProcedureType procType)
        {
            _procType = procType;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcedureInstruction)));
        }

        // Internal

        ProcedureType _procType = ProcedureType.ThreePens;

        // UI events

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, Navigation.Test);
        }

        private void Practice_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, Navigation.Practice);
        }

        private void Familiarize_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, Navigation.Familiarization);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, Navigation.Back);
        }
    }
}
