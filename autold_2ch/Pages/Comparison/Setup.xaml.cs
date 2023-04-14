using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Olfactory2Ch.Comm;
using Olfactory2Ch.Tests.Comparison;

namespace Olfactory2Ch.Pages.Comparison
{
    public partial class Setup : Page, IPage<Settings>, Tests.ITestEmulator, INotifyPropertyChanged
    {
        public event EventHandler<Settings> Next;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsHumanSniffer { get; private set; } = true;

        public Setup()
        {
            InitializeComponent();

            Storage.Instance
                .BindScaleToZoomLevel(sctScale)
                .BindVisibilityToDebug(lblDebug);

            DataContext = this;

            cmbGasSniffer.ItemsSource = Enum.GetValues(typeof(GasSniffer));

            cmbGasSniffer.SelectedItem = _settings.Sniffer;
            txbFreshAirFlow.Text = _settings.FreshAirFlow.ToString("F1");
            txbPracticeOdorFlow.Text = _settings.PracticeOdorFlow.ToString("F1");
            txbTestOdorFlow.Text = _settings.TestOdorFlow.ToString("F1");
            txbInitialPause.Text = _settings.InitialPause.ToString();
            txbOdorFlowDuration.Text = _settings.OdorFlowDuration.ToString();
            txbPairsOfMixtures.Text = _settings.SerializeMixtures();
            chkWaitForPID.IsChecked = _settings.WaitForPID;
            //txbPIDSamplingInterval.Text = _settings.PIDReadingInterval.ToString();
        }

        public void EmulationInit() { }

        public void EmulationFinilize() { }


        // Internal

        readonly Settings _settings = new();

        private Utils.Validation CheckInput()
        {
            var pairsOfMixtures = Settings.ParsePairsOfMixtures(txbPairsOfMixtures.Text.Replace("\r\n", "\n"), out string error);
            if (pairsOfMixtures == null)
            {
                return new Utils.Validation(txbPairsOfMixtures, error);
            }

            var validations = new List<Utils.Validation>
            {
                new Utils.Validation(txbFreshAirFlow, 1, 10, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbPracticeOdorFlow, 1, 80, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbTestOdorFlow, 1, 80, Utils.Validation.ValueFormat.Float),
                new Utils.Validation(txbInitialPause, 0, 10000, Utils.Validation.ValueFormat.Integer),
                new Utils.Validation(txbOdorFlowDuration, 0.1, MFC.MAX_SHORT_PULSE_DURATION / 1000, Utils.Validation.ValueFormat.Float),
                //new Utils.Validation(txbPIDSamplingInterval, 100, 5000, Utils.Validation.ValueFormat.Integer),
            };

            foreach (var v in validations)
            {
                if (!v.IsValid)
                {
                    return v;
                }
            }

            return null;
        }


        // UI events

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var validation = CheckInput();
            if (validation != null)
            {
                var msg = Utils.L10n.T("CorrectAndTryAgain");
                Utils.MsgBox.Error(App.Name, $"{validation}.\n{msg}");
                validation.Source.Focus();
                validation.Source.SelectAll();
            }
            else
            {
                _settings.Sniffer = (GasSniffer)cmbGasSniffer.SelectedItem;
                _settings.FreshAirFlow = double.Parse(txbFreshAirFlow.Text);
                _settings.PracticeOdorFlow = double.Parse(txbPracticeOdorFlow.Text);
                _settings.TestOdorFlow = double.Parse(txbTestOdorFlow.Text);
                _settings.InitialPause = int.Parse(txbInitialPause.Text);
                _settings.OdorFlowDuration = double.Parse(txbOdorFlowDuration.Text);
                _settings.PairsOfMixtures = Settings.ParsePairsOfMixtures(txbPairsOfMixtures.Text.Replace("\r\n", "\n"), out string _);
                _settings.WaitForPID = chkWaitForPID.IsChecked ?? false;
                //_settings.PIDReadingInterval = int.Parse(txbPIDSamplingInterval.Text);

                _settings.Save();

                OlfactoryDeviceModel.Gas1 = _settings.Gas1;
                OlfactoryDeviceModel.Gas2 = _settings.Gas2;

                Next?.Invoke(this, _settings);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Next?.Invoke(this, null);
        }

        private void GasSniffer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsHumanSniffer = (GasSniffer)cmbGasSniffer.SelectedItem == GasSniffer.Human;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHumanSniffer)));
        }
    }
}
